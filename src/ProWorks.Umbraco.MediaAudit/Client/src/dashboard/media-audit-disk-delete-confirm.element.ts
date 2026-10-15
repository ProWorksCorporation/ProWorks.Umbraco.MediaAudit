import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import type { DiskMediaItem } from "../api/media-audit.repository.js";

/**
 * Disk-Only Media Cleanup, User Story 2/3 (FR-007): confirmation step before permanently deleting
 * disk-only files. There is no Recycle Bin for these - they were never Umbraco media items - so
 * this is worded and gated like <media-audit-purge-confirm> (an explicit acknowledgment checkbox),
 * not the lighter, reversible wording of <media-audit-delete-confirm>.
 *
 * Purely presentational - the actual POST /disk-delete call and skip-reporting live in
 * media-audit-disk-panel.element.ts.
 */
@customElement("media-audit-disk-delete-confirm")
export class MediaAuditDiskDeleteConfirmElement extends UmbElementMixin(LitElement) {
  @property({ attribute: false })
  items: DiskMediaItem[] = [];

  @property({ type: Boolean })
  confirming = false;

  @property({ attribute: false })
  onConfirm?: () => void;

  @property({ attribute: false })
  onCancel?: () => void;

  @state()
  private _acknowledged = false;

  render() {
    return html`
      <uui-box class="confirm-box" headline="Permanently delete ${this.items.length} file(s) from disk?">
        <p class="warning">
          These files aren't Umbraco media items, so there's no Recycle Bin to recover them from -
          this permanently removes them from disk and cannot be undone.
        </p>
        <ul>
          ${this.items.map((item) => html`<li>${item.relativePath}</li>`)}
        </ul>
        <label class="acknowledge">
          <uui-checkbox
            ?checked=${this._acknowledged}
            @change=${(e: Event) => (this._acknowledged = (e.target as HTMLInputElement).checked)}
          ></uui-checkbox>
          I understand this action is permanent and cannot be undone.
        </label>
        <div class="actions">
          <uui-button look="secondary" ?disabled=${this.confirming} @click=${() => this.onCancel?.()}>
            Cancel
          </uui-button>
          <uui-button
            look="primary"
            color="danger"
            .state=${this.confirming ? "waiting" : undefined}
            ?disabled=${this.confirming || !this._acknowledged}
            @click=${() => this.onConfirm?.()}
          >
            Permanently delete ${this.items.length} file(s)
          </uui-button>
        </div>
      </uui-box>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        margin-bottom: var(--uui-size-space-5);
      }

      .confirm-box {
        border: 2px solid var(--uui-color-danger, #d42054);
      }

      .warning {
        color: var(--uui-color-danger-standalone, #d42054);
        font-weight: bold;
      }

      ul {
        max-height: 200px;
        overflow-y: auto;
        margin: var(--uui-size-space-3) 0;
      }

      .acknowledge {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .actions {
        display: flex;
        justify-content: flex-end;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-4);
      }
    `,
  ];
}

export default MediaAuditDiskDeleteConfirmElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-disk-delete-confirm": MediaAuditDiskDeleteConfirmElement;
  }
}
