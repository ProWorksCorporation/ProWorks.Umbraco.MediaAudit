using System.Runtime.CompilerServices;

// Lets ProWorks.Umbraco.MediaAudit.Tests.Unit reference internal types (e.g. DeletionLogService.DeletionLogRow)
// for Moq setups against generic IUmbracoDatabase methods that need a concrete, nameable type
// argument - it's not part of the package's public API surface.
[assembly: InternalsVisibleTo("ProWorks.Umbraco.MediaAudit.Tests.Unit")]
