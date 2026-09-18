import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import type { DeletionLogItem } from "../api/media-audit.repository.js";

/**
 * User Story 4 (P4): confirmation step before permanently purging already-deleted items (FR-018).
 * Distinct from <media-audit-delete-confirm> and deliberately more strongly worded, since purge is
 * irreversible - requires ticking an explicit acknowledgment checkbox before the button enables,
 * not just a click.
 *
 * Purely presentational - the actual POST /purge call and skip-reporting live in the dashboard
 * (media-audit-deletion-log.element.ts, which owns which deletion-log entry this is purging from).
 */
@customElement("media-audit-purge-confirm")
export class MediaAuditPurgeConfirmElement extends UmbElementMixin(LitElement) {
  @property({ attribute: false })
  items: DeletionLogItem[] = [];

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
      <uui-box class="confirm-box" headline="Permanently purge ${this.items.length} item(s)?">
        <p class="warning">
          This <strong>permanently</strong> removes these files. There is no Recycle Bin to recover
          them from afterward - this cannot be undone.
        </p>
        <ul>
          ${this.items.map((item) => html`<li>${item.name}</li>`)}
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
            Permanently purge ${this.items.length} item(s)
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

export default MediaAuditPurgeConfirmElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-purge-confirm": MediaAuditPurgeConfirmElement;
  }
}
