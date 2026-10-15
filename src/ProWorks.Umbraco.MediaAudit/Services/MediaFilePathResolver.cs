using System.Text.Json;
using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Resolves a media item's stored file path from its built-in <c>umbracoFile</c> property - a plain
/// string for built-in upload types, or a JSON blob with a "src" field for image-cropper-backed
/// types. Shared by <see cref="MediaReferenceScanner"/> (usage scan) and
/// <see cref="IDiskMediaAuditService"/> (disk-only detection, research.md R2), so both stay
/// consistent about what "this media item's file" means.
/// </summary>
internal static class MediaFilePathResolver
{
    public static string? GetFilePath(IMedia media)
    {
        var raw = media.GetValue<string>(CoreConstants.Conventions.Media.File);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.TrimStart().StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement.TryGetProperty("src", out var srcElement)
                    ? srcElement.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return raw;
    }
}
