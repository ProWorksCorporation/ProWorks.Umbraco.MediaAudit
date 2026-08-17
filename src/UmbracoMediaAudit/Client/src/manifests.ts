import { manifests as dashboardManifests } from "./dashboard/manifests.js";

// Job of the bundle is to collate all the manifests from different parts of the extension.
// This bundle is loaded from umbraco-package.json.
export const manifests: Array<UmbExtensionManifest> = [
  ...dashboardManifests,
];
