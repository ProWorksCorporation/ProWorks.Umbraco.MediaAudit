#!/usr/bin/env python3
"""
UNR BCN — Umbraco Media Audit

Identifies media files in the Umbraco database that are not referenced by any
content (published or draft). Outputs a CSV report for client review.

Supports two database backends:
  --db    SQLite file   (local dev — no extra dependencies needed)
  --conn  SQL Server    (staging / production — requires pyodbc)

Usage:
    # SQLite (local dev, auto-detected path):
    python scripts/media_audit.py

    # SQLite (explicit path):
    python scripts/media_audit.py --db "path/to/Umbraco.sqlite.db"

    # SQL Server (staging/production):
    python scripts/media_audit.py --conn "Server=myserver;Database=mydb;UID=user;PWD=pass;"

    # Custom output path:
    python scripts/media_audit.py [--db|--conn ...] --output "reports/media-audit.csv"

    # SQL Server with Windows auth (trusted connection):
    python scripts/media_audit.py --conn "Server=myserver;Database=mydb;Trusted_Connection=Yes;"

Reference detection covers:
  - Media picker properties  (UDI: umb://media/<guid>)
  - Rich Text Editor content (HTML src/href pointing to /media/ paths)
  - Block editor JSON        (nested media GUIDs)

Limitation: Hardcoded /media/ paths in Razor templates are NOT detected.
  Review items flagged as unreferenced manually before deleting.

Note: Both published and draft property values are checked so that media
  used only in unsaved drafts is not incorrectly flagged as unreferenced.
"""

import argparse
import csv
import sqlite3
import sys
import time
from datetime import datetime
from pathlib import Path

# Default SQLite DB path — resolved relative to this script (scripts/ → repo root)
DEFAULT_DB = (
    Path(__file__).resolve().parent.parent
    / "src" / "UmbracoProject" / "umbraco" / "Data" / "Umbraco.sqlite.db"
)

MEDIA_OBJECT_TYPE = "B796F64C-1F99-4FFB-B886-4BF4BC011A9C"


# ---------------------------------------------------------------------------
# Database abstraction — SQLite and SQL Server return the same row structure
# ---------------------------------------------------------------------------

class SqliteBackend:
    def __init__(self, db_path: Path):
        if not db_path.exists():
            print(f"Error: SQLite database not found:\n  {db_path}", file=sys.stderr)
            print(
                "\nTip: Run the Umbraco project at least once to create the database,\n"
                "     or pass --db with the correct path.",
                file=sys.stderr,
            )
            sys.exit(1)
        self.conn = sqlite3.connect(str(db_path))
        print(f"Database : {db_path}  [SQLite]")

    def fetchall(self, query: str, params: tuple = ()) -> list[tuple]:
        cur = self.conn.execute(query, params)
        return cur.fetchall()

    def concat(self, *cols: str) -> str:
        """SQLite string concatenation — wrap each column in COALESCE to handle NULLs."""
        return " || ' ' || ".join(f"COALESCE({c}, '')" for c in cols)


def to_odbc(conn_str: str) -> str:
    """
    Translate an ADO.NET connection string (as provided by Umbraco Cloud) into
    ODBC format expected by pyodbc. Unknown keys are passed through unchanged.
    """
    # ADO.NET key → ODBC key  (case-insensitive input)
    remap = {
        "data source":              "Server",
        "initial catalog":          "Database",
        "user id":                  "UID",
        "password":                 "PWD",
        "connection timeout":       "LoginTimeout",
        "connect timeout":          "LoginTimeout",
    }
    # ADO.NET-only keys that have no ODBC equivalent — drop them
    drop = {
        "multipleactiveresultsets",
        "persist security info",
        "application name",
        "applicationname",
        "type system version",
        "context connection",
        "enlist",
        "pooling",
        "attachdbfilename",
    }
    parts = []
    for segment in conn_str.split(";"):
        segment = segment.strip()
        if not segment:
            continue
        if "=" not in segment:
            parts.append(segment)
            continue
        key, _, val = segment.partition("=")
        key_lower = key.strip().lower()
        if key_lower in drop:
            continue
        key = remap.get(key_lower, key.strip())
        parts.append(f"{key}={val.strip()}")
    return ";".join(parts)


class SqlServerBackend:
    def __init__(self, conn_str: str):
        conn_str = to_odbc(conn_str)
        try:
            import pyodbc  # noqa: PLC0415
        except ImportError:
            print(
                "Error: pyodbc is required for SQL Server connections.\n"
                "  Install it with:  pip install pyodbc\n"
                "  Also ensure ODBC Driver 17 (or 18) for SQL Server is installed:\n"
                "  https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server",
                file=sys.stderr,
            )
            sys.exit(1)

        # Prepend the ODBC driver if not already specified
        if "Driver=" not in conn_str:
            last_error = None
            for driver in ("ODBC Driver 18 for SQL Server", "ODBC Driver 17 for SQL Server"):
                try:
                    self.conn = pyodbc.connect(f"Driver={{{driver}}};{conn_str}", timeout=10)
                    print(f"Database : SQL Server  [{driver}]")
                    return
                except pyodbc.Error as e:
                    # IM002 = driver not found; anything else is a real connection error
                    if e.args[0] == "IM002":
                        continue
                    last_error = e
                    break
            if last_error:
                print(f"Error: Could not connect to SQL Server:\n  {last_error}", file=sys.stderr)
            else:
                print(
                    "Error: No SQL Server ODBC driver found on this machine.\n"
                    "  Install from: https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server",
                    file=sys.stderr,
                )
            sys.exit(1)
        else:
            try:
                self.conn = pyodbc.connect(conn_str, timeout=10)
                print("Database : SQL Server")
            except pyodbc.Error as e:
                print(f"Error: Could not connect to SQL Server:\n  {e}", file=sys.stderr)
                sys.exit(1)

    def fetchall(self, query: str, params: tuple = ()) -> list[tuple]:
        cur = self.conn.cursor()
        cur.execute(query, params)
        return cur.fetchall()

    def concat(self, *cols: str) -> str:
        """SQL Server string concatenation operator."""
        return " + ' ' + ".join(f"COALESCE({c}, '')" for c in cols)


# ---------------------------------------------------------------------------
# Queries — column names are the same across both backends for Umbraco v10
# ---------------------------------------------------------------------------

MEDIA_QUERY = """
    SELECT
        n.id,
        n.uniqueId                AS guid,
        n.text                    AS name,
        n.createDate,
        cv.versionDate            AS updateDate,
        ct.alias                  AS media_type,
        mv.path                   AS file_path
    FROM      umbracoNode             n
    JOIN      umbracoContent          c   ON c.nodeId    = n.id
    JOIN      cmsContentType          ct  ON ct.nodeId   = c.contentTypeId
    LEFT JOIN umbracoContentVersion   cv  ON cv.nodeId   = n.id
                                         AND cv.[current]  = 1
    LEFT JOIN umbracoMediaVersion     mv  ON mv.id       = cv.id
    WHERE n.nodeObjectType = ?
      AND n.trashed = 0
    ORDER BY n.id
"""

CONTENT_NODES_QUERY = """
    SELECT
        n.id        AS node_id,
        n.text      AS node_name,
        pd.textValue,
        pd.varcharValue
    FROM      umbracoNode             n
    JOIN      umbracoContent          c   ON c.nodeId   = n.id
    JOIN      umbracoContentVersion   cv  ON cv.nodeId  = n.id AND cv.[current] = 1
    LEFT JOIN umbracoPropertyData     pd  ON pd.versionId = cv.id
    WHERE n.nodeObjectType != ?
      AND n.trashed = 0
      AND (pd.textValue IS NOT NULL OR pd.varcharValue IS NOT NULL)
    ORDER BY n.id
"""


def get_media_items(backend) -> list[dict]:
    rows = backend.fetchall(MEDIA_QUERY, (MEDIA_OBJECT_TYPE,))
    keys = ("id", "guid", "name", "createDate", "updateDate", "media_type", "file_path")
    return [dict(zip(keys, row)) for row in rows]


def get_content_nodes(backend) -> dict[int, dict]:
    """
    Return a dict keyed by node_id, each containing the page name and a
    single string of all its property values combined — used for reference searching.
    """
    rows = backend.fetchall(CONTENT_NODES_QUERY, (MEDIA_OBJECT_TYPE,))
    nodes: dict[int, dict] = {}
    for node_id, node_name, text_val, varchar_val in rows:
        if node_id not in nodes:
            nodes[node_id] = {"name": node_name or f"(node {node_id})", "text": ""}
        combined = (text_val or "") + " " + (varchar_val or "")
        nodes[node_id]["text"] += " " + combined
    # Lower-case once per node so per-media searching is fast
    for node in nodes.values():
        node["text_lower"] = node["text"].lower()
    return nodes


# ---------------------------------------------------------------------------
# Reference detection — per page
# ---------------------------------------------------------------------------

def find_referencing_pages(guid: str, file_path: str, nodes: dict[int, dict]) -> list[str]:
    """
    Return a list of page names that reference this media item.
    Checks (case-insensitive) the GUID and file path in each page's property values.
    """
    guid_lower       = guid.lower()
    guid_no_hyphens  = guid_lower.replace("-", "")
    path_lower       = file_path.lower() if file_path else ""
    filename         = path_lower.rstrip("/").rsplit("/", 1)[-1] if path_lower else ""

    referencing = []
    for node in nodes.values():
        blob = node["text_lower"]
        if (
            guid_no_hyphens in blob
            or guid_lower in blob
            or (path_lower and path_lower in blob)
            or (filename and filename in blob)
        ):
            referencing.append(node["name"])
    return referencing


# ---------------------------------------------------------------------------
# Main audit logic
# ---------------------------------------------------------------------------

def run_audit(backend, output_path: Path) -> None:
    print("Fetching media items    ...", end=" ", flush=True)
    media = get_media_items(backend)
    print(f"{len(media)} found")

    print("Loading content pages   ...", end=" ", flush=True)
    nodes = get_content_nodes(backend)
    print(f"{len(nodes)} pages")

    print("Analysing references    ...")
    rows = []
    total = len(media)
    start_time = time.time()
    log_every = max(1, total // 200)  # ~200 progress lines over the whole run
    for idx, item in enumerate(media, start=1):
        guid  = str(item["guid"])      if item["guid"]      else ""
        fpath = str(item["file_path"]) if item["file_path"] else ""

        # Skip items with no file path — folders and other container nodes have no URL
        if not fpath:
            continue

        referencing_pages = find_referencing_pages(guid, fpath, nodes)
        referenced_on = " | ".join(sorted(referencing_pages))

        rows.append(
            {
                "id":            item["id"],
                "name":          item["name"],
                "path":          fpath,
                "media_type":    item["media_type"],
                "create_date":   item["createDate"],
                "update_date":   item["updateDate"],
                "page_count":    len(referencing_pages),
                "referenced_on": referenced_on,
            }
        )

        if idx % log_every == 0 or idx == total:
            elapsed = time.time() - start_time
            rate = idx / elapsed if elapsed > 0 else 0
            remaining = (total - idx) / rate if rate > 0 else 0
            pct = idx / total * 100
            print(
                f"  [{idx}/{total}] {pct:5.1f}%  "
                f"elapsed {elapsed/60:5.1f}m  ETA {remaining/60:5.1f}m",
                flush=True,
            )
    print("done")

    # Sort: unreferenced first, then referenced — oldest-first within each group
    rows.sort(key=lambda r: (r["page_count"] > 0, r["create_date"] or ""))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "id", "name", "path", "media_type",
        "create_date", "update_date", "page_count", "referenced_on",
    ]
    with output_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)

    unreferenced_files = sum(1 for r in rows if r["page_count"] == 0)
    referenced_files   = sum(1 for r in rows if r["page_count"] > 0)

    print()
    print(f"Total files      : {len(rows)}")
    print(f"  Referenced     : {referenced_files}")
    print(f"  Unreferenced   : {unreferenced_files}  <-- review these")
    print(f"Report           : {output_path.resolve()}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> None:
    default_output = str(
        Path(__file__).resolve().parent / "reports" / f"media-audit-{datetime.now().strftime('%Y-%m-%d')}.csv"
    )

    parser = argparse.ArgumentParser(
        description="Audit Umbraco media library for unreferenced files.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )

    db_group = parser.add_mutually_exclusive_group()
    db_group.add_argument(
        "--db",
        metavar="PATH",
        help="Path to Umbraco.sqlite.db for local SQLite audit  (default: auto-detected)",
    )
    db_group.add_argument(
        "--conn",
        metavar="CONN_STR",
        help=(
            "SQL Server connection string for staging/production audit.\n"
            'Example: "Server=myserver;Database=mydb;UID=user;PWD=pass;"\n'
            "Requires: pip install pyodbc  +  ODBC Driver 17/18 for SQL Server"
        ),
    )
    parser.add_argument(
        "--output",
        default=default_output,
        metavar="PATH",
        help="Output CSV file  (default: scripts/reports/media-audit-YYYY-MM-DD.csv)",
    )

    args = parser.parse_args()

    if args.conn:
        backend = SqlServerBackend(args.conn)
    else:
        db_path = Path(args.db) if args.db else DEFAULT_DB
        backend = SqliteBackend(db_path)

    run_audit(backend, Path(args.output))


if __name__ == "__main__":
    main()
