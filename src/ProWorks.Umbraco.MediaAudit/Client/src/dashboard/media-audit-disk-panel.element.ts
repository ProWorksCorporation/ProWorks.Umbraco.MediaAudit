import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
  nothing,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import {
  MediaAuditRepository,
  type DiskAuditRun,
  type DiskMediaItem,
} from "../api/media-audit.repository.js";
import "./media-audit-disk-delete-confirm.element.js";

const POLL_INTERVAL_MS = 1500;

/**
 * Disk-Only Media Cleanup (spec 002-disk-media-cleanup): a second, independent audit mode toggled
 * from the main dashboard toolbar (same pattern as the existing Deletion Log toggle). Scans
 * Umbraco's media file system for files no Umbraco media item owns (FR-001-FR-003), and lets an
 * admin delete selected files or all of them at once (FR-005, FR-006) - permanently, since these
 * were never Umbraco media items and so have no Recycle Bin.
 *
 * `isAdmin` is passed down from the parent dashboard, which already derives it from whether
 * GET /deletion-log succeeds - not re-derived here.
 */
@customElement("media-audit-disk-panel")
export class MediaAuditDiskPanelElement extends UmbElementMixin(LitElement) {
  @property({ type: Boolean })
  isAdmin = false;

  @state()
  private _run?: DiskAuditRun;

  @state()
  private _items: DiskMediaItem[] = [];

  @state()
  private _isScanning = false;

  @state()
  private _selected = new Set<string>();

  @state()
  private _deleteConfirmOpen = false;

  @state()
  private _deleting = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #pollHandle?: ReturnType<typeof setTimeout>;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notificationContext = context;
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    this.#loadSummary();
    this.#loadItems();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    if (this.#pollHandle) {
      clearTimeout(this.#pollHandle);
    }
  }

  async #loadSummary() {
    try {
      this._run = await MediaAuditRepository.getDiskSummary();
      this._isScanning = this._run.status === "Running";
    } catch (error) {
      console.error("[media-audit] failed to load disk summary", error);
    }
  }

  async #loadItems() {
    try {
      const response = await MediaAuditRepository.getDiskItems();
      this._items = response.items;
    } catch (error) {
      console.error("[media-audit] failed to load disk items", error);
    }
  }

  #onScan = async () => {
    this._isScanning = true;
    try {
      this._run = await MediaAuditRepository.runDiskScan();
      this.#pollUntilComplete();
    } catch (error) {
      this._isScanning = false;
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Could not start the disk scan",
          message: error instanceof Error ? error.message : String(error),
        },
      });
    }
  };

  #pollUntilComplete() {
    this.#pollHandle = setTimeout(async () => {
      await this.#loadSummary();

      if (this._run?.status === "Running") {
        this.#pollUntilComplete();
        return;
      }

      this._isScanning = false;
      this._selected = new Set();
      await this.#loadItems();

      if (this._run?.status === "Failed") {
        this.#notificationContext?.peek("danger", {
          data: {
            headline: "Disk scan failed",
            message: this._run.errorMessage ?? "An unexpected error occurred while scanning disk media.",
          },
        });
      } else {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Disk scan complete",
            message: `${this._run?.diskOnlyCount ?? 0} file(s) on disk aren't in Umbraco.`,
          },
        });
      }
    }, POLL_INTERVAL_MS);
  }

  #onToggleSelect = (e: Event, item: DiskMediaItem) => {
    const checked = (e.target as HTMLInputElement).checked;
    const next = new Set(this._selected);
    if (checked) {
      next.add(item.relativePath);
    } else {
      next.delete(item.relativePath);
    }
    this._selected = next;
  };

  #onToggleSelectAll = (e: Event) => {
    const checked = (e.target as HTMLInputElement).checked;
    this._selected = checked ? new Set(this._items.map((item) => item.relativePath)) : new Set();
  };

  #onDeleteSelectedClick = () => {
    this._deleteConfirmOpen = true;
  };

  #onDeleteAllClick = () => {
    this._selected = new Set(this._items.map((item) => item.relativePath));
    this._deleteConfirmOpen = true;
  };

  #onDeleteCancel = () => {
    this._deleteConfirmOpen = false;
  };

  #onDeleteConfirm = async () => {
    this._deleting = true;
    try {
      const result = await MediaAuditRepository.deleteDiskItems([...this._selected]);
      this._deleteConfirmOpen = false;
      this._selected = new Set();

      if (result.skipped.length > 0) {
        this.#notificationContext?.peek("warning", {
          data: {
            headline: "Some files were skipped",
            message: `${result.deleted.length} deleted. ${result.skipped.length} skipped - they've either already been removed or now belong to a media item.`,
          },
        });
      } else {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Delete complete",
            message: `${result.deleted.length} file(s) permanently removed from disk.`,
          },
        });
      }

      await this.#loadSummary();
      await this.#loadItems();
    } catch (error) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Delete failed",
          message: error instanceof Error ? error.message : String(error),
        },
      });
    } finally {
      this._deleting = false;
    }
  };

  #formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    const units = ["KB", "MB", "GB", "TB"];
    let value = bytes / 1024;
    let unitIndex = 0;
    while (value >= 1024 && unitIndex < units.length - 1) {
      value /= 1024;
      unitIndex++;
    }
    return `${value.toFixed(1)} ${units[unitIndex]}`;
  }

  #formatDate(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  render() {
    return html`
      <uui-box headline="Disk-Only Media">
        <div slot="header">Find files on disk that aren't in Umbraco</div>

        <p class="disclaimer">
          Scans Umbraco's media storage for files with no matching Umbraco media item - active or
          in the Recycle Bin. Deleting a file here is permanent: there's no Recycle Bin for files
          Umbraco never knew about.
        </p>

        <div class="toolbar">
          <uui-button
            look="primary"
            color="default"
            .state=${this._isScanning ? "waiting" : undefined}
            ?disabled=${this._isScanning}
            @click=${this.#onScan}
          >
            Scan Disk Media
          </uui-button>

          ${this.isAdmin
            ? html`
                <uui-button
                  look="secondary"
                  color="danger"
                  ?disabled=${this._selected.size === 0}
                  @click=${this.#onDeleteSelectedClick}
                >
                  Delete Selected (${this._selected.size})
                </uui-button>
                <uui-button
                  look="secondary"
                  color="danger"
                  ?disabled=${this._items.length === 0}
                  @click=${this.#onDeleteAllClick}
                >
                  Delete All
                </uui-button>
              `
            : nothing}

          ${this._isScanning ? html`<uui-loader></uui-loader>` : nothing}

          <span class="last-refreshed">
            ${this._run?.runAt
              ? html`Last scanned: ${this.#formatDate(this._run.runAt)} - ${this._run.diskOnlyCount} disk-only
                (${this.#formatBytes(this._run.diskOnlySizeBytes)})`
              : html`No disk scan has run yet this session.`}
          </span>
        </div>

        ${this._deleteConfirmOpen
          ? html`
              <media-audit-disk-delete-confirm
                .items=${this._items.filter((item) => this._selected.has(item.relativePath))}
                .confirming=${this._deleting}
                .onConfirm=${this.#onDeleteConfirm}
                .onCancel=${this.#onDeleteCancel}
              ></media-audit-disk-delete-confirm>
            `
          : nothing}

        ${this.#renderTable()}
      </uui-box>
    `;
  }

  #renderTable() {
    if (this._items.length === 0) {
      return html`<p>No disk-only files found.</p>`;
    }

    const allSelected = this._items.every((item) => this._selected.has(item.relativePath));
    const someSelected = !allSelected && this._items.some((item) => this._selected.has(item.relativePath));

    return html`
      <div class="grid ${this.isAdmin ? "has-checkbox" : ""}" role="table">
        <div class="grid-row grid-header" role="row">
          ${this.isAdmin
            ? html`
                <div class="grid-cell" role="columnheader">
                  <uui-checkbox
                    ?checked=${allSelected}
                    .indeterminate=${someSelected}
                    @change=${this.#onToggleSelectAll}
                  ></uui-checkbox>
                </div>
              `
            : nothing}
          <div class="grid-cell" role="columnheader">Relative path</div>
          <div class="grid-cell" role="columnheader">Size</div>
          <div class="grid-cell" role="columnheader">Last modified</div>
          <div class="grid-cell" role="columnheader"></div>
        </div>

        ${this._items.map(
          (item) => html`
            <div class="grid-row" role="row">
              ${this.isAdmin
                ? html`
                    <div class="grid-cell" role="cell">
                      <uui-checkbox
                        ?checked=${this._selected.has(item.relativePath)}
                        @change=${(e: Event) => this.#onToggleSelect(e, item)}
                      ></uui-checkbox>
                    </div>
                  `
                : nothing}
              <div class="grid-cell" role="cell">${item.relativePath}</div>
              <div class="grid-cell" role="cell">${this.#formatBytes(item.sizeBytes)}</div>
              <div class="grid-cell" role="cell">${this.#formatDate(item.lastModifiedUtc)}</div>
              <div class="grid-cell" role="cell">
                <uui-button look="secondary" compact href=${item.url} target="_blank" rel="noopener">
                  Open
                </uui-button>
              </div>
            </div>
          `
        )}
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        margin-bottom: var(--uui-size-space-5);
      }

      .disclaimer {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .toolbar {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-5);
        flex-wrap: wrap;
      }

      .last-refreshed {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .grid {
        display: grid;
        grid-template-columns: 3fr 100px 160px 80px;
        width: 100%;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: var(--uui-border-radius, 3px);
        overflow: hidden;
      }

      .grid.has-checkbox {
        grid-template-columns: 40px 3fr 100px 160px 80px;
      }

      .grid-row {
        display: contents;
      }

      .grid-cell {
        display: flex;
        align-items: center;
        padding: var(--uui-size-space-3) var(--uui-size-space-4);
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
        background-color: var(--uui-color-surface, #fff);
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .grid-header .grid-cell {
        font-weight: bold;
        background-color: var(--uui-color-surface-alt, #f6f6f6);
        white-space: normal;
      }
    `,
  ];
}

export default MediaAuditDiskPanelElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-disk-panel": MediaAuditDiskPanelElement;
  }
}
