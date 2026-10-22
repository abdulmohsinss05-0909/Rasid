namespace Rasid.Domain.Posts;

/// <summary>
/// The first media attachment of a post, for display only. The image is referenced, never stored.
/// <see cref="AltText"/> is kept so the attachment stays accessible to screen readers.
/// </summary>
public sealed record PostMedia(string Kind, string PreviewUrl, string? AltText)
{
    public const int KindMaxLength = 16;
    public const int UrlMaxLength = 512;
    public const int AltTextMaxLength = 1000;

    public static PostMedia? Create(string? kind, string? previewUrl, string? altText)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(previewUrl) || previewUrl.Length > UrlMaxLength)
        {
            return null;
        }

        var alt = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        return new PostMedia(
            kind.Length > KindMaxLength ? kind[..KindMaxLength] : kind,
            previewUrl,
            alt is { Length: > AltTextMaxLength } ? alt[..AltTextMaxLength] : alt);
    }
}
