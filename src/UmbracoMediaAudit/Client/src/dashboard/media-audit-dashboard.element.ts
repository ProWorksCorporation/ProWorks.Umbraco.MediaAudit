import {
  LitElement,
  css,
  html,
  customElement,
  state,
  nothing,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import {
  MediaAuditRepository,
  setAuthContext,
  type AuditRun,
  type MediaAuditItem,
  type MediaUsageStatus,
} from "../api/media-audit.repository.js";
// Side-effect import - registers <media-audit-detail>, used in #renderDetailRow below.
import "./media-audit-detail.element.js";

const POLL_INTERVAL_MS = 1500;

/**
 * User Story 1 (P1, MVP): shows the Used/Unused audit list with per-item metadata, a progress
 * indicator while an audit runs, and the "detectable references only" disclaimer (FR-001, FR-002,
 * FR-006, FR-010, FR-011, FR-016).
 *
 * User Story 2 (P2): selecting a "Used" item expands <media-audit-detail>'s usage drill-down
 * directly under that row (an accordion, not a panel appended after the whole list - with
 * potentially thousands of items, jumping to the bottom of the page doesn't scale). The summary
 * pills (Total/Used/Unused) double as the status filter so "Used" items are reachable at all -
 * this is a minimal, hard-coded status switch, deliberately NOT the full filter control set
 * (status/type/folder) that is User Story 3's job (tasks.md T035).
 *
 * The results list is hand-rolled CSS Grid (each row uses `display: contents` so its cells
 * participate directly in the grid's column tracks) rather than `uui-table`/`uui-table-row` -
 * that component has no colspan equivalent, which the accordion row needs to span every column.
 *
 * Delete/purge/deletion-log controls (User Story 4) are wired in as a later phase lands.
 */
@customElement("media-audit-dashboard")
export class MediaAuditDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _run?: AuditRun;

  @state()
  private _items: MediaAuditItem[] = [];

  /** undefined = "Total" (no filter). */
  @state()
  private _statusFilter: MediaUsageStatus | undefined = "Unused";

  @state()
  private _selectedItem?: MediaAuditItem;

  @state()
  private _isRunning = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #pollHandle?: ReturnType<typeof setTimeout>;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notificationContext = context;
    });
    // Loading is deferred until the auth context arrives (see setAuthContext in
    // media-audit.repository.ts) so the first request carries a Bearer token instead of 401ing.
    this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
      if (!context) return;
      setAuthContext(context);
      this.#loadSummary();
      this.#loadItems();
    });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    if (this.#pollHandle) {
      clearTimeout(this.#pollHandle);
    }
  }

  async #loadSummary() {
    try {
      this._run = await MediaAuditRepository.getSummary();
      this._isRunning = this._run.status === "Running";
    } catch (error) {
      console.error("[media-audit] failed to load summary", error);
    }
  }

  async #loadItems() {
    try {
      const response = await MediaAuditRepository.getItems(this._statusFilter);
      this._items = response.items;
    } catch (error) {
      console.error("[media-audit] failed to load items", error);
    }
  }

  #onStatusFilterChange = (status: MediaUsageStatus | undefined) => {
    if (this._statusFilter === status) return;
    this._statusFilter = status;
    this._selectedItem = undefined;
    this.#loadItems();
  };

  #onRunAudit = async () => {
    this._isRunning = true;
    try {
      this._run = await MediaAuditRepository.runAudit();
      this.#pollUntilComplete();
    } catch (error) {
      this._isRunning = false;
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Could not start the audit",
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

      this._isRunning = false;
      await this.#loadItems();

      if (this._run?.status === "Failed") {
        this.#notificationContext?.peek("danger", {
          data: {
            headline: "Audit failed",
            message: this._run.errorMessage ?? "An unexpected error occurred while auditing media.",
          },
        });
      } else {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Audit complete",
            message: `${this._run?.usedCount ?? 0} used, ${this._run?.unusedCount ?? 0} unused.`,
          },
        });
      }
    }, POLL_INTERVAL_MS);
  }

  #onSelectItem(item: MediaAuditItem) {
    this._selectedItem = this._selectedItem?.key === item.key ? undefined : item;
  }

  #formatBytes(bytes: number | null): string {
    if (bytes === null) return "—";
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
      <uui-box headline="Media Audit">
        <div slot="header">Find unused and used media</div>

        <p class="disclaimer">
          Usage detection covers standard, trackable Umbraco content references only. References
          embedded in free text, external systems, or non-standard custom property editors may not
          be detected.
        </p>

        <div class="toolbar">
          <uui-button
            look="primary"
            color="default"
            .state=${this._isRunning ? "waiting" : undefined}
            ?disabled=${this._isRunning}
            @click=${this.#onRunAudit}
          >
            Run Audit
          </uui-button>

          ${this._isRunning ? html`<uui-loader></uui-loader>` : nothing}

          <span class="last-refreshed">
            ${this._run?.runAt
              ? html`Last refreshed: ${this.#formatDate(this._run.runAt)}`
              : html`No audit has run yet this session.`}
          </span>
        </div>

        ${this._run ? this.#renderSummary(this._run) : nothing}

        ${this.#renderTable()}
      </uui-box>
    `;
  }

  #renderSummary(run: AuditRun) {
    // Doubles as the status filter (a minimal one - see class doc) - click a pill to filter to it.
    const pill = (status: MediaUsageStatus | undefined, color: "default" | "positive" | "warning", label: string) => html`
      <uui-tag
        class="filter-pill"
        color=${color}
        ?active=${this._statusFilter === status}
        @click=${() => this.#onStatusFilterChange(status)}
      >
        ${label}
      </uui-tag>
    `;

    return html`
      <div class="summary">
        ${pill(undefined, "default", `Total: ${run.totalScanned}`)}
        ${pill("Used", "positive", `Used: ${run.usedCount} (${this.#formatBytes(run.usedSizeBytes)})`)}
        ${pill("Unused", "warning", `Unused: ${run.unusedCount} (${this.#formatBytes(run.unusedSizeBytes)})`)}
      </div>
    `;
  }

  #renderTable() {
    if (this._items.length === 0) {
      return html`<p>No ${(this._statusFilter ?? "").toLowerCase() || "matching"} media found.</p>`;
    }

    return html`
      <div class="grid" role="table">
        <div class="grid-row grid-header" role="row">
          <div class="grid-cell" role="columnheader">Name</div>
          <div class="grid-cell" role="columnheader">Type</div>
          <div class="grid-cell" role="columnheader">Size</div>
          <div class="grid-cell" role="columnheader">Folder</div>
          <div class="grid-cell" role="columnheader">Last modified</div>
          <div class="grid-cell" role="columnheader"></div>
        </div>

        ${this._items.map((item) => {
          const isSelected = this._selectedItem?.key === item.key;
          return html`
            <div
              class="grid-row ${isSelected ? "selected" : ""}"
              role="row"
              @click=${() => this.#onSelectItem(item)}
            >
              <div class="grid-cell" role="cell">${item.name}</div>
              <div class="grid-cell" role="cell">${item.mediaTypeAlias}</div>
              <div class="grid-cell" role="cell">${this.#formatBytes(item.sizeBytes)}</div>
              <div class="grid-cell" role="cell">${item.path}</div>
              <div class="grid-cell" role="cell">${this.#formatDate(item.updateDate)}</div>
              <div class="grid-cell" role="cell">
                <uui-button
                  look="secondary"
                  compact
                  href=${item.mediaEditUrl}
                  @click=${(e: Event) => e.stopPropagation()}
                >
                  Open
                </uui-button>
              </div>
            </div>
            ${isSelected ? this.#renderDetailRow(item) : nothing}
          `;
        })}
      </div>
    `;
  }

  /** Accordions directly under the clicked row (a full-width grid cell), not appended after the whole list. */
  #renderDetailRow(item: MediaAuditItem) {
    return html`
      <div class="grid-row" role="row">
        <div class="grid-cell detail-cell" role="cell">
          <dl>
            <dt>Type</dt>
            <dd>${item.mediaTypeAlias}${item.extension ? html` (.${item.extension})` : nothing}</dd>
            <dt>Size</dt>
            <dd>${this.#formatBytes(item.sizeBytes)}</dd>
            <dt>Folder</dt>
            <dd>${item.path}</dd>
            <dt>Last modified</dt>
            <dd>${this.#formatDate(item.updateDate)}</dd>
          </dl>

          ${item.usageStatus === "Used" ? html`<media-audit-detail .item=${item}></media-audit-detail>` : nothing}
        </div>
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
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
      }

      .last-refreshed {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .summary {
        display: flex;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-5);
      }

      .filter-pill {
        cursor: pointer;
        opacity: 0.55;
        transition: opacity 0.1s ease-in-out;
      }

      .filter-pill:hover {
        opacity: 0.8;
      }

      .filter-pill[active] {
        opacity: 1;
        outline: 2px solid var(--uui-color-selected, var(--uui-color-current, #1b264f));
        outline-offset: 2px;
        border-radius: var(--uui-border-radius, 3px);
      }

      /* Hand-rolled table (see class doc for why this isn't uui-table). */
      .grid {
        display: grid;
        grid-template-columns: 2fr 1fr 100px 2fr 160px 80px;
        width: 100%;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: var(--uui-border-radius, 3px);
        overflow: hidden;
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

      .grid-row:not(.grid-header):hover .grid-cell {
        cursor: pointer;
        background-color: var(--uui-color-surface-emphasis, #f3f3f5);
      }

      .grid-row.selected .grid-cell {
        background-color: var(--uui-color-selected, #3544b1);
        color: var(--uui-color-selected-contrast, #fff);
      }

      .detail-cell {
        grid-column: 1 / -1;
        cursor: default;
        white-space: normal;
        display: block;
      }

      .detail-cell dl {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-space-2) var(--uui-size-space-4);
        margin: 0 0 var(--uui-size-space-4) 0;
      }

      .detail-cell dt {
        font-weight: bold;
      }

      .detail-cell dd {
        margin: 0;
      }
    `,
  ];
}

export default MediaAuditDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-dashboard": MediaAuditDashboardElement;
  }
}
