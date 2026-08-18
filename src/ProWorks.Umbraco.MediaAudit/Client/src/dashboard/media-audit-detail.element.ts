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
import { MediaAuditRepository, type MediaAuditItem, type MediaUsageReference } from "../api/media-audit.repository.js";

/**
 * User Story 2 (P2): for a "Used" media item, shows every content item that references it - culture,
 * publish state, and detection source - each with a working link to open it (FR-004, FR-005, FR-017,
 * Acceptance Scenarios 1-2). Fetches lazily on demand (contracts §GET /items/{key}/usages), not
 * precomputed for every row in the results table.
 */
@customElement("media-audit-detail")
export class MediaAuditDetailElement extends UmbElementMixin(LitElement) {
  @property({ attribute: false })
  item?: MediaAuditItem;

  @state()
  private _usages?: MediaUsageReference[];

  @state()
  private _loading = false;

  @state()
  private _error?: string;

  override updated(changedProperties: Map<string, unknown>): void {
    if (changedProperties.has("item") && this.item) {
      this.#loadUsages(this.item.key);
    }
  }

  async #loadUsages(mediaKey: string) {
    this._loading = true;
    this._error = undefined;
    this._usages = undefined;
    try {
      const response = await MediaAuditRepository.getUsages(mediaKey);
      this._usages = response.usages;
    } catch (error) {
      this._error = error instanceof Error ? error.message : String(error);
    } finally {
      this._loading = false;
    }
  }

  render() {
    if (!this.item) return nothing;

    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }

    if (this._error) {
      return html`<uui-tag color="danger">Could not load usages: ${this._error}</uui-tag>`;
    }

    if (!this._usages) return nothing;

    if (this._usages.length === 0) {
      return html`
        <uui-tag color="warning">
          Marked "Used" but no active references could be found. This can happen when the
          referencing content has since been deleted - consider re-running the audit.
        </uui-tag>
      `;
    }

    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Content</uui-table-head-cell>
          <uui-table-head-cell>Type</uui-table-head-cell>
          <uui-table-head-cell>Culture</uui-table-head-cell>
          <uui-table-head-cell>Status</uui-table-head-cell>
          <uui-table-head-cell>Detected via</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${this._usages.map(
          (usage) => html`
            <uui-table-row>
              <uui-table-cell>${usage.contentName}</uui-table-cell>
              <uui-table-cell>${usage.contentTypeAlias}</uui-table-cell>
              <uui-table-cell>${usage.culture ?? "—"}</uui-table-cell>
              <uui-table-cell>
                <uui-tag color=${usage.publishState === "Published" ? "positive" : "default"}>
                  ${usage.publishState}
                </uui-tag>
              </uui-table-cell>
              <uui-table-cell>${usage.detectionSource}</uui-table-cell>
              <uui-table-cell>
                <uui-button look="secondary" compact href=${usage.editUrl}>Open</uui-button>
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
        margin-top: var(--uui-size-space-4);
      }
    `,
  ];
}

export default MediaAuditDetailElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-audit-detail": MediaAuditDetailElement;
  }
}
