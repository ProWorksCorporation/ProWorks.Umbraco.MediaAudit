namespace UmbracoMediaAudit.Services;

/// <summary>Small shared helper for constructing backoffice deep links (contracts/media-audit-api.md).</summary>
public static class BackofficeLinks
{
    public static string ContentEditUrl(Guid contentKey) =>
        $"/umbraco/section/content/workspace/document/edit/{contentKey}";
}
