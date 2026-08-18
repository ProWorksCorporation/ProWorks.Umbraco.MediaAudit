export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Media Audit Dashboard",
    alias: "ProWorks.Umbraco.MediaAudit.Dashboard",
    type: "dashboard",
    js: () => import("./media-audit-dashboard.element.js"),
    meta: {
      label: "Media Audit",
      pathname: "media-audit",
    },
    conditions: [
      {
        // FR-001: dashboard lives in the Media section. FR-013 (Media-section access) is enforced
        // server-side by the API's [Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)] -
        // this condition only controls where the tab appears in the backoffice UI.
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Media",
      },
    ],
  },
];
