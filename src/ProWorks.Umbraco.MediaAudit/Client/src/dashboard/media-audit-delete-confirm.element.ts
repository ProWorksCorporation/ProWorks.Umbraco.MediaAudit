import {
  LitElement,
  css,
  html,
  customElement,
  property,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import type { MediaAuditItem } from "../api/media-audit.repository.js";

/**
 * User Story 4 (P4): confirmation step before deleting selected "Unused" items (FR-014). Deletion
 * moves items to the Recycle Bin - reversible, not permanent - which this deliberately says plainly
 * so it reads as a lighter confirmation than <media-audit-purge-confirm>'s.
 *
 * Purely presentational - the actual POST /delete call and skip-reporting live in the dashboard,
 * which passes `confirming` down and receives clicks back via the onConfirm/onCancel callbacks.
 */
@customElement("media-audit-delete-confirm")
export class MediaAuditDeleteConfirmElement extends UmbElementMixin(LitElement) {
  @property({ attribute: false })
  items: MediaAuditItem[] = [];

  @property({ type: Boolean })
  confirming = false;

  @property({ attribute: false })
  onConfirm?: () => void;

  @property({ attribute: false })
  onCancel?: () => void;

  render() {
    return html`
      <uui-box class="confirm-box" headline="Delete ${this.items.length} item(s)?">
        <p>
          These will be moved to Umbraco's Recycle Bin - reversible via the standard Media section,
          not permanently removed. To reclaim disk space immediately afterward, use Purge from the
          deletion log.
        </p>
        <ul>
          ${this.items.map((item) => html`<li>${item.name}</li>`)}
        </ul>
        <div class="actions">
          <uui-button look="secondary" ?disabled=${this.confirming} @click=${() => this.onCancel?.()}>
            Cancel
          </uui-button>
          <uui-button
            look="primary"
            color="danger"
            .state=${this.confirming ? "waiting" : undefined}
            ?disabled=${this.confirming}
            @click=${() => this.onConfirm?.()}
          >
            Delete ${this.items.length} item(s)
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

      ul {
        max-height: 200px;
        overflow-y: auto;
        margin: var(--uui-size-space-3) 0;
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

export default MediaAuditDeleteConfirmElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-delete-confirm": MediaAuditDeleteConfirmElement;
  }
}
