namespace UmbracoMediaAudit.Models;

/// <summary>One selectable option for the type filter dropdown (FR-007) - alias for filtering, name for display.</summary>
public sealed class MediaTypeOption
{
    public required string Alias { get; init; }

    public required string Name { get; init; }
}
