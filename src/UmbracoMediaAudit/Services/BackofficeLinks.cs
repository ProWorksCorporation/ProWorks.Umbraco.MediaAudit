namespace UmbracoMediaAudit.Services;

/// <summary>Small shared helper for constructing backoffice deep links (contracts/media-audit-api.md).</summary>
public static class BackofficeLinks
{
    public static string ContentEditUrl(Guid contentKey) =>
        $"/umbraco/section/content/workspace/document/edit/{contentKey}";

    /// <summary>Deep link to open the media item itself (not a referencing content item) in the Media section.</summary>
    public static string MediaEditUrl(Guid mediaKey) =>
        $"/umbraco/section/media/workspace/media/edit/{mediaKey}";
}
