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
} from "../api/media-audit.repository.js";

const POLL_INTERVAL_MS = 1500;

/**
 * User Story 1 (P1, MVP): shows the Used/Unused audit list with per-item metadata, a progress
 * indicator while an audit runs, and the "detectable references only" disclaimer (FR-001, FR-002,
 * FR-006, FR-010, FR-011, FR-016).
 *
 * Delete/purge/deletion-log controls (User Story 4) and usage-detail drill-down (User Story 2) are
 * wired in as later phases land - see media-audit-detail.element.ts, media-audit-delete-confirm
 * element.ts, etc.
 */
@customElement("media-audit-dashboard")
export class MediaAuditDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _run?: AuditRun;

  @state()
  private _items: MediaAuditItem[] = [];

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
      const response = await MediaAuditRepository.getItems("Unused");
      this._items = response.items;
    } catch (error) {
      console.error("[media-audit] failed to load items", error);
    }
  }

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

        ${this._selectedItem ? this.#renderDetailPanel(this._selectedItem) : nothing}
      </uui-box>
    `;
  }

  #renderSummary(run: AuditRun) {
    return html`
      <div class="summary">
        <uui-tag color="default">Total: ${run.totalScanned}</uui-tag>
        <uui-tag color="positive">Used: ${run.usedCount} (${this.#formatBytes(run.usedSizeBytes)})</uui-tag>
        <uui-tag color="warning">Unused: ${run.unusedCount} (${this.#formatBytes(run.unusedSizeBytes)})</uui-tag>
      </div>
    `;
  }

  #renderTable() {
    if (this._items.length === 0) {
      return html`<p>No unused media found.</p>`;
    }

    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Name</uui-table-head-cell>
          <uui-table-head-cell>Type</uui-table-head-cell>
          <uui-table-head-cell>Size</uui-table-head-cell>
          <uui-table-head-cell>Folder</uui-table-head-cell>
          <uui-table-head-cell>Last modified</uui-table-head-cell>
        </uui-table-head>
        ${this._items.map(
          (item) => html`
            <uui-table-row
              ?selected=${this._selectedItem?.key === item.key}
              @click=${() => this.#onSelectItem(item)}
            >
              <uui-table-cell>${item.name}</uui-table-cell>
              <uui-table-cell>${item.mediaTypeAlias}</uui-table-cell>
              <uui-table-cell>${this.#formatBytes(item.sizeBytes)}</uui-table-cell>
              <uui-table-cell>${item.path}</uui-table-cell>
              <uui-table-cell>${this.#formatDate(item.updateDate)}</uui-table-cell>
            </uui-table-row>
          `
        )}
      </uui-table>
    `;
  }

  #renderDetailPanel(item: MediaAuditItem) {
    return html`
      <uui-box class="detail-panel" headline=${item.name}>
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
      </uui-box>
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

      uui-table-row {
        cursor: pointer;
      }

      .detail-panel {
        margin-top: var(--uui-size-layout-1);
      }

      dl {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-space-2) var(--uui-size-space-4);
      }

      dt {
        font-weight: bold;
      }

      dd {
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
