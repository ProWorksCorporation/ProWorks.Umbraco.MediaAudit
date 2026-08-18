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
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Media",
      },
    ],
  },
];
