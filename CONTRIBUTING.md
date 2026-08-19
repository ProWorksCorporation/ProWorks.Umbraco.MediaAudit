# Contributing

This repo's own local Umbraco sample site, spec-kit documents, and manual test fixtures live under
`src/` and `specs/001-media-usage-audit/`. See
[specs/001-media-usage-audit/quickstart.md](specs/001-media-usage-audit/quickstart.md) for the full
local setup and validation walkthrough.

## Running the sample site

```powershell
# Sample site
dotnet run --project src/ProWorks.Umbraco.MediaAudit.Web

# Backoffice client assets (separate terminal)
cd src/ProWorks.Umbraco.MediaAudit/Client
npm install
npm run dev
```

## Tests

```powershell
dotnet test tests/ProWorks.Umbraco.MediaAudit.Tests.Unit
dotnet test tests/ProWorks.Umbraco.MediaAudit.Tests.Integration

cd tests/ProWorks.Umbraco.MediaAudit.Client.Tests
npx web-test-runner
```

## Project governance

Development follows the principles in [.specify/memory/constitution.md](.specify/memory/constitution.md)
(Umbraco package standards, documentation-driven verification, UUI/Lit/TypeScript for backoffice UI,
Vite as the sole build tool, and minimal/self-documenting code over inline commentary).
