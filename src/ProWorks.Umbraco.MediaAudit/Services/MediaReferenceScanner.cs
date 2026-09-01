using System.Text.Json;
using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IMediaReferenceScanner" />
public sealed class MediaReferenceScanner : IMediaReferenceScanner
{
    private const int PageSize = 500;

    private readonly IContentService _contentService;

    public MediaReferenceScanner(IContentService contentService)
    {
        _contentService = contentService;
    }

    public Task<IReadOnlyList<MediaUsageReference>> FindReferencesAsync(IMedia media, CancellationToken cancellationToken = default)
    {
        var guidWithHyphens = media.Key.ToString().ToLowerInvariant();
        var guidNoHyphens = media.Key.ToString("N").ToLowerInvariant();
        var filePath = GetMediaFilePath(media)?.ToLowerInvariant();
        var fileName = filePath is null ? null : filePath.TrimEnd('/').Split('/').LastOrDefault();

        var results = new List<MediaUsageReference>();

        long pageIndex = 0;
        long total;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = _contentService
                .GetPagedDescendants(CoreConstants.System.Root, pageIndex, PageSize, out total)
                .ToList();

            foreach (var content in page)
            {
                var cultures = content.AvailableCultures.Any()
                    ? content.AvailableCultures.Cast<string?>()
                    : new[] { (string?)null };

                foreach (var culture in cultures)
                {
                    foreach (var property in content.Properties)
                    {
                        var raw = property.GetValue(culture, published: false);
                        if (raw is null)
                        {
                            continue;
                        }

                        var text = raw.ToString();
                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        var lower = text.ToLowerInvariant();
                        var isMatch =
                            lower.Contains(guidNoHyphens) ||
                            lower.Contains(guidWithHyphens) ||
                            (filePath is not null && lower.Contains(filePath)) ||
                            (fileName is not null && lower.Contains(fileName));

                        if (!isMatch)
                        {
                            continue;
                        }

                        results.Add(new MediaUsageReference
                        {
                            ContentId = content.Id,
                            ContentKey = content.Key,
                            ContentName = content.Name ?? $"(content {content.Id})",
                            ContentTypeAlias = content.ContentType.Alias,
                            Culture = culture,
                            PropertyAlias = property.Alias,
                            PublishState = content.Published ? ContentPublishState.Published : ContentPublishState.Draft,
                            DetectionSource = MediaUsageDetectionSource.Scan,
                            EditUrl = BackofficeLinks.ContentEditUrl(content.Key),
                        });
                    }
                }
            }

            pageIndex++;
        } while (pageIndex * PageSize < total);

        return Task.FromResult<IReadOnlyList<MediaUsageReference>>(results);
    }

    /// <summary>
    /// Resolves the media item's stored file path. Built-in media types store this as a plain string
    /// in "umbracoFile"; image-cropper-backed types store a JSON blob with a "src" field.
    /// </summary>
    private static string? GetMediaFilePath(IMedia media)
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
