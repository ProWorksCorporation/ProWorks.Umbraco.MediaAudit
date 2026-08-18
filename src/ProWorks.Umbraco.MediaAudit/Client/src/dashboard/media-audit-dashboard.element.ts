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
  MediaAuditApiError,
  MediaAuditRepository,
  setAuthContext,
  type AuditRun,
  type MediaAuditItem,
  type MediaAuditItemsQuery,
  type MediaAuditSortField,
  type MediaAuditSortDirection,
  type MediaFolder,
  type MediaTypeOption,
  type MediaUsageStatus,
} from "../api/media-audit.repository.js";
// Side-effect imports - register the custom elements used below.
import "./media-audit-detail.element.js";
import "./media-audit-delete-confirm.element.js";
import "./media-audit-deletion-log.element.js";

const POLL_INTERVAL_MS = 1500;

/**
 * User Story 1 (P1, MVP): shows the Used/Unused audit list with per-item metadata, a progress
 * indicator while an audit runs, and the "detectable references only" disclaimer (FR-001, FR-002,
 * FR-006, FR-010, FR-011, FR-016).
 *
 * User Story 2 (P2): selecting a "Used" item expands <media-audit-detail>'s usage drill-down
 * directly under that row (an accordion, not a panel appended after the whole list - with
 * potentially thousands of items, jumping to the bottom of the page doesn't scale). The summary
 * pills (Total/Used/Unused) double as the status filter so "Used" items are reachable at all.
 *
 * User Story 3 (P3): type/folder filter dropdowns, sortable column headers, and a CSV export
 * button (FR-007, FR-008, FR-009) - all sharing the same MediaAuditItemsQuery the server filters/
 * sorts by (GET /items, GET /export).
 *
 * The results list is hand-rolled CSS Grid (each row uses `display: contents` so its cells
 * participate directly in the grid's column tracks) rather than `uui-table`/`uui-table-row` -
 * that component has no colspan equivalent, which the accordion row needs to span every column.
 *
 * User Story 4 (P4, admin-only): selecting "Unused" items and deleting them (moves to Recycle Bin,
 * with a mandatory fresh re-check per item), plus a Deletion Log view purge actions are initiated
 * from (see media-audit-deletion-log.element.ts). Admin status isn't read from Umbraco's own
 * client-side user context - it's derived from whether GET /deletion-log itself succeeds or 403s,
 * reusing the server's own authorization rather than re-deriving "is admin" independently. Per
 * FR-015 these controls are hidden entirely for non-admins, not merely disabled.
 */
@customElement("media-audit-dashboard")
export class MediaAuditDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _run?: AuditRun;

  @state()
  private _items: MediaAuditItem[] = [];

  @state()
  private _totalItems = 0;

  /** undefined = "Total" (no filter). */
  @state()
  private _statusFilter: MediaUsageStatus | undefined = "Unused";

  @state()
  private _mediaTypeFilter?: string;

  @state()
  private _folderFilter?: number;

  @state()
  private _mediaTypeOptions: MediaTypeOption[] = [];

  @state()
  private _folders: MediaFolder[] = [];

  @state()
  private _sort: MediaAuditSortField = "name";

  @state()
  private _sortDirection: MediaAuditSortDirection = "asc";

  @state()
  private _selectedItem?: MediaAuditItem;

  @state()
  private _isRunning = false;

  @state()
  private _isExporting = false;

  @state()
  private _isAdmin = false;

  @state()
  private _selectedForDelete = new Set<string>();

  @state()
  private _deleteConfirmOpen = false;

  @state()
  private _deleting = false;

  @state()
  private _showDeletionLog = false;

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
      this.#loadFilterOptions();
      this.#checkAdmin();
    });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    if (this.#pollHandle) {
      clearTimeout(this.#pollHandle);
    }
  }

  #currentQuery(): MediaAuditItemsQuery {
    return {
      status: this._statusFilter,
      mediaTypeAlias: this._mediaTypeFilter,
      folderId: this._folderFilter,
      sort: this._sort,
      sortDirection: this._sortDirection,
    };
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
      const response = await MediaAuditRepository.getItems(this.#currentQuery());
      this._items = response.items;
      this._totalItems = response.totalItems;
    } catch (error) {
      console.error("[media-audit] failed to load items", error);
    }
  }

  async #loadFilterOptions() {
    try {
      const [folders, mediaTypes] = await Promise.all([
        MediaAuditRepository.getFolders(),
        MediaAuditRepository.getMediaTypeOptions(),
      ]);
      this._folders = folders.folders;
      this._mediaTypeOptions = mediaTypes.mediaTypes;
    } catch (error) {
      console.error("[media-audit] failed to load filter options", error);
    }
  }

  /**
   * Admin status is derived from whether GET /deletion-log itself succeeds or 403s, rather than
   * read from Umbraco's own client-side user context - see class doc comment. A non-403 error
   * (network blip, etc.) is treated the same as "not admin" - a safe default that just hides the
   * controls rather than risking showing them to someone who isn't actually one.
   */
  async #checkAdmin() {
    try {
      await MediaAuditRepository.getDeletionLog(1, 1);
      this._isAdmin = true;
    } catch (error) {
      if (!(error instanceof MediaAuditApiError) || error.status !== 403) {
        console.error("[media-audit] admin check failed unexpectedly", error);
      }
      this._isAdmin = false;
    }
  }

  #onStatusFilterChange = (status: MediaUsageStatus | undefined) => {
    if (this._statusFilter === status) return;
    this._statusFilter = status;
    this._selectedItem = undefined;
    this._selectedForDelete = new Set();
    this.#loadItems();
  };

  #onMediaTypeFilterChange = (e: Event) => {
    const value = (e.target as HTMLSelectElement).value;
    this._mediaTypeFilter = value || undefined;
    this._selectedItem = undefined;
    this.#loadItems();
  };

  #onFolderFilterChange = (e: Event) => {
    const value = (e.target as HTMLSelectElement).value;
    this._folderFilter = value ? Number(value) : undefined;
    this._selectedItem = undefined;
    this.#loadItems();
  };

  #onSortChange = (field: MediaAuditSortField) => {
    if (this._sort === field) {
      this._sortDirection = this._sortDirection === "asc" ? "desc" : "asc";
    } else {
      this._sort = field;
      this._sortDirection = "asc";
    }
    this.#loadItems();
  };

  #onExport = async () => {
    this._isExporting = true;
    try {
      await MediaAuditRepository.exportCsv(this.#currentQuery());
    } catch (error) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Export failed",
          message: error instanceof Error ? error.message : String(error),
        },
      });
    } finally {
      this._isExporting = false;
    }
  };

  /**
   * uui-checkbox fires "change" (UUIBooleanInputEvent), not "click" - a plain @click listener on it
   * never fires the way a native <input> would, since the component's internal click handling
   * doesn't bubble a matching semantic here. @click is still needed separately (see the template)
   * purely to stop the row's own click-to-select-item handler from also firing.
   */
  #onToggleSelectForDelete = (e: Event, item: MediaAuditItem) => {
    const checked = (e.target as HTMLInputElement).checked;
    const next = new Set(this._selectedForDelete);
    if (checked) {
      next.add(item.key);
    } else {
      next.delete(item.key);
    }
    this._selectedForDelete = next;
  };

  #onToggleSelectAllForDelete = (e: Event) => {
    const checked = (e.target as HTMLInputElement).checked;
    this._selectedForDelete = checked ? new Set(this._items.map((item) => item.key)) : new Set();
  };

  #onDeleteSelectedClick = () => {
    this._deleteConfirmOpen = true;
  };

  #onDeleteCancel = () => {
    this._deleteConfirmOpen = false;
  };

  #onDeleteConfirm = async () => {
    this._deleting = true;
    try {
      const result = await MediaAuditRepository.deleteItems([...this._selectedForDelete]);
      this._deleteConfirmOpen = false;
      this._selectedForDelete = new Set();

      // Skip-reporting feedback (spec.md race-condition edge case): an item that became referenced
      // since the last audit is skipped, not deleted or errored as a whole batch - surface that
      // distinctly rather than a generic "done".
      if (result.skipped.length > 0) {
        this.#notificationContext?.peek("warning", {
          data: {
            headline: "Some items were skipped",
            message: `${result.deleted.length} deleted. ${result.skipped.length} skipped - they've become referenced by content since the last audit.`,
          },
        });
      } else {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Delete complete",
            message: `${result.deleted.length} item(s) moved to the Recycle Bin.`,
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

  #onToggleDeletionLog = () => {
    this._showDeletionLog = !this._showDeletionLog;
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
      await this.#loadFilterOptions();

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

          <uui-button
            look="secondary"
            .state=${this._isExporting ? "waiting" : undefined}
            ?disabled=${this._isExporting || this._totalItems === 0}
            @click=${this.#onExport}
          >
            Export CSV
          </uui-button>

          ${this._isAdmin && this._statusFilter === "Unused"
            ? html`
                <uui-button
                  look="secondary"
                  color="danger"
                  ?disabled=${this._selectedForDelete.size === 0}
                  @click=${this.#onDeleteSelectedClick}
                >
                  Delete Selected (${this._selectedForDelete.size})
                </uui-button>
              `
            : nothing}
          ${this._isAdmin
            ? html`
                <uui-button look="secondary" @click=${this.#onToggleDeletionLog}>
                  ${this._showDeletionLog ? "Hide" : "View"} Deletion Log
                </uui-button>
              `
            : nothing}

          ${this._isRunning ? html`<uui-loader></uui-loader>` : nothing}

          <span class="last-refreshed">
            ${this._run?.runAt
              ? html`Last refreshed: ${this.#formatDate(this._run.runAt)}`
              : html`No audit has run yet this session.`}
          </span>
        </div>

        ${this._isAdmin && this._showDeletionLog ? html`<media-audit-deletion-log></media-audit-deletion-log>` : nothing}

        <div class="filter-bar">
          ${this._run ? this.#renderSummary(this._run) : nothing}
          ${this.#renderFilters()}
        </div>

        ${this._deleteConfirmOpen
          ? html`
              <media-audit-delete-confirm
                .items=${this._items.filter((item) => this._selectedForDelete.has(item.key))}
                .confirming=${this._deleting}
                .onConfirm=${this.#onDeleteConfirm}
                .onCancel=${this.#onDeleteCancel}
              ></media-audit-delete-confirm>
            `
          : nothing}

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

  #renderFilters() {
    return html`
      <div class="filters">
        <label>
          Type
          <select .value=${this._mediaTypeFilter ?? ""} @change=${this.#onMediaTypeFilterChange}>
            <option value="">All types</option>
            ${this._mediaTypeOptions.map((type) => html`<option value=${type.alias}>${type.name}</option>`)}
          </select>
        </label>

        <label>
          Folder
          <select .value=${this._folderFilter?.toString() ?? ""} @change=${this.#onFolderFilterChange}>
            <option value="">All folders</option>
            ${this._folders.map((folder) => html`<option value=${folder.id}>${folder.path}</option>`)}
          </select>
        </label>
      </div>
    `;
  }

  #renderSortableHeader(field: MediaAuditSortField, label: string) {
    const isActive = this._sort === field;
    const indicator = isActive ? (this._sortDirection === "asc" ? "▲" : "▼") : "";
    return html`
      <button class="sort-header" @click=${() => this.#onSortChange(field)}>
        ${label} <span class="sort-indicator">${indicator}</span>
      </button>
    `;
  }

  #renderTable() {
    if (this._items.length === 0) {
      return html`<p>No ${(this._statusFilter ?? "").toLowerCase() || "matching"} media found.</p>`;
    }

    // Delete is only offered for "Unused" items (FR-014) and only to admins (FR-015) - hidden
    // entirely, not just disabled, for everyone else.
    const showCheckboxes = this._isAdmin && this._statusFilter === "Unused";
    const allSelected = showCheckboxes && this._items.length > 0 && this._items.every((item) => this._selectedForDelete.has(item.key));
    const someSelected = showCheckboxes && !allSelected && this._items.some((item) => this._selectedForDelete.has(item.key));

    return html`
      <div class="grid ${showCheckboxes ? "has-checkbox" : ""}" role="table">
        <div class="grid-row grid-header" role="row">
          ${showCheckboxes
            ? html`
                <div class="grid-cell" role="columnheader">
                  <uui-checkbox
                    ?checked=${allSelected}
                    .indeterminate=${someSelected}
                    @change=${this.#onToggleSelectAllForDelete}
                  ></uui-checkbox>
                </div>
              `
            : nothing}
          <div class="grid-cell" role="columnheader">${this.#renderSortableHeader("name", "Name")}</div>
          <div class="grid-cell" role="columnheader">Type</div>
          <div class="grid-cell" role="columnheader">${this.#renderSortableHeader("sizeBytes", "Size")}</div>
          <div class="grid-cell" role="columnheader">Folder</div>
          <div class="grid-cell" role="columnheader">${this.#renderSortableHeader("updateDate", "Last modified")}</div>
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
              ${showCheckboxes
                ? html`
                    <div class="grid-cell" role="cell" @click=${(e: Event) => e.stopPropagation()}>
                      <uui-checkbox
                        ?checked=${this._selectedForDelete.has(item.key)}
                        @change=${(e: Event) => this.#onToggleSelectForDelete(e, item)}
                      ></uui-checkbox>
                    </div>
                  `
                : nothing}
              <div class="grid-cell" role="cell">${item.name}</div>
              <div class="grid-cell" role="cell">${item.mediaTypeName}</div>
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
            <dd>${item.mediaTypeName}${item.extension ? html` (.${item.extension})` : nothing}</dd>
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

      .filter-bar {
        display: flex;
        /* The pills have no label above them (unlike the Type/Folder dropdowns), so aligning tops
           leaves them sitting higher than the dropdowns themselves - align bottoms instead. */
        align-items: flex-end;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: var(--uui-size-space-4);
        padding-bottom: var(--uui-size-space-5);
        margin-bottom: var(--uui-size-space-5);
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
      }

      .summary {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: var(--uui-size-space-4);
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

      .filters {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-5);
      }

      .filters label {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-1);
        font-size: var(--uui-type-small-size);
        font-weight: bold;
      }

      .filters select {
        font-family: inherit;
        font-size: var(--uui-type-default-size, 15px);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: var(--uui-border-radius, 3px);
        min-width: 180px;
      }

      .sort-header {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-1);
        background: none;
        border: none;
        padding: 0;
        margin: 0;
        font: inherit;
        font-weight: bold;
        cursor: pointer;
        color: inherit;
      }

      .sort-indicator {
        font-size: 0.7em;
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

      /* Extra leading checkbox column - only present for admins viewing "Unused" (User Story 4). */
      .grid.has-checkbox {
        grid-template-columns: 40px 2fr 1fr 100px 2fr 160px 80px;
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

      /* :not(.selected) matters here, not just style - ":hover" gives this selector higher
         specificity than ".grid-row.selected" below, so without it, hovering a selected row would
         win the background-color fight (flipping navy back to light gray) while the selected
         row's white text stays (untouched by this rule) - invisible white-on-light-gray text. */
      .grid-row:not(.grid-header):not(.selected):hover .grid-cell {
        cursor: pointer;
        background-color: var(--uui-color-surface-emphasis, #f3f3f5);
      }

      .grid-row.selected:hover .grid-cell {
        cursor: pointer;
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
