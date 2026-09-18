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
import { MediaAuditRepository, type DeletionLogEntry } from "../api/media-audit.repository.js";
import "./media-audit-purge-confirm.element.js";

/**
 * User Story 4 (P4, admin-only): the batch-level accountability log (FR-019) - one row per
 * delete/purge action, never per item. Also where purging happens from: FR-018's "specific items
 * previously deleted via FR-014" are exactly a Delete entry's own item list, so each Delete entry
 * gets its own Purge action here rather than a separate item-picker UI.
 */
@customElement("media-audit-deletion-log")
export class MediaAuditDeletionLogElement extends UmbElementMixin(LitElement) {
  @state()
  private _entries: DeletionLogEntry[] = [];

  @state()
  private _loading = false;

  @state()
  private _error?: string;

  @state()
  private _purgeConfirmEntry?: DeletionLogEntry;

  @state()
  private _purging = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notificationContext = context;
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    this.#load();
  }

  async #load() {
    this._loading = true;
    this._error = undefined;
    try {
      const response = await MediaAuditRepository.getDeletionLog();
      this._entries = response.entries;
    } catch (error) {
      this._error = error instanceof Error ? error.message : String(error);
    } finally {
      this._loading = false;
    }
  }

  #onPurgeClick(entry: DeletionLogEntry) {
    this._purgeConfirmEntry = entry;
  }

  #onPurgeCancel = () => {
    this._purgeConfirmEntry = undefined;
  };

  #onPurgeConfirm = async () => {
    const entry = this._purgeConfirmEntry;
    if (!entry) return;

    this._purging = true;
    try {
      const result = await MediaAuditRepository.purgeItems(entry.items.map((item) => item.key));
      this._purgeConfirmEntry = undefined;

      if (result.skipped.length > 0) {
        this.#notificationContext?.peek("warning", {
          data: {
            headline: "Some items were skipped",
            message: `${result.purged.length} purged. ${result.skipped.length} skipped - already restored from the Recycle Bin since being deleted.`,
          },
        });
      } else {
        this.#notificationContext?.peek("positive", {
          data: {
            headline: "Purge complete",
            message: `${result.purged.length} item(s) permanently removed.`,
          },
        });
      }

      await this.#load();
    } catch (error) {
      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Purge failed",
          message: error instanceof Error ? error.message : String(error),
        },
      });
    } finally {
      this._purging = false;
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
      <uui-box headline="Deletion Log">
        <div slot="header">Every delete and purge action, one row per batch</div>

        ${this._loading && this._entries.length === 0 ? html`<uui-loader></uui-loader>` : nothing}
        ${this._error ? html`<uui-tag color="danger">Could not load deletion log: ${this._error}</uui-tag>` : nothing}
        ${!this._loading && !this._error && this._entries.length === 0
          ? html`<p>No delete or purge actions recorded yet.</p>`
          : nothing}
        ${this._entries.length > 0 ? this.#renderTable() : nothing}

        ${this._purgeConfirmEntry
          ? html`
              <media-audit-purge-confirm
                .items=${this._purgeConfirmEntry.items}
                .confirming=${this._purging}
                .onConfirm=${this.#onPurgeConfirm}
                .onCancel=${this.#onPurgeCancel}
              ></media-audit-purge-confirm>
            `
          : nothing}
      </uui-box>
    `;
  }

  #renderTable() {
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Date</uui-table-head-cell>
          <uui-table-head-cell>Action</uui-table-head-cell>
          <uui-table-head-cell>Admin</uui-table-head-cell>
          <uui-table-head-cell>Items</uui-table-head-cell>
          <uui-table-head-cell>Size</uui-table-head-cell>
          <uui-table-head-cell>Skipped</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${this._entries.map(
          (entry) => html`
            <uui-table-row>
              <uui-table-cell>${this.#formatDate(entry.occurredAt)}</uui-table-cell>
              <uui-table-cell>
                <uui-tag color=${entry.actionType === "Purge" ? "danger" : "warning"}>${entry.actionType}</uui-tag>
              </uui-table-cell>
              <uui-table-cell>User #${entry.performedByUserId}</uui-table-cell>
              <uui-table-cell>${entry.itemCount}</uui-table-cell>
              <uui-table-cell>${this.#formatBytes(entry.totalSizeBytes)}</uui-table-cell>
              <uui-table-cell>${entry.skippedCount}</uui-table-cell>
              <uui-table-cell>
                ${entry.actionType === "Delete" && entry.itemCount > 0
                  ? html`
                      <uui-button look="secondary" compact @click=${() => this.#onPurgeClick(entry)}>
                        Purge
                      </uui-button>
                    `
                  : nothing}
              </uui-table-cell>
            </uui-table-row>
          `
        )}
      </uui-table>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        margin-top: var(--uui-size-layout-1);
        margin-bottom: var(--uui-size-layout-1);
      }

      uui-table {
        margin-top: var(--uui-size-space-5);
        --uui-table-cell-padding: var(--uui-size-space-4) var(--uui-size-space-5);
      }

      uui-table-row:not(:last-of-type) {
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
      }
    `,
  ];
}

export default MediaAuditDeletionLogElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-deletion-log": MediaAuditDeletionLogElement;
  }
}
