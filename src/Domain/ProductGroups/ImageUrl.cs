namespace Kart.Product.Domain.ProductGroups;

/// <summary>
/// A product's photo (BRD "no product without a real image"). Protects the one invariant that
/// actually matters at this layer - it must be a real, loadable absolute http(s) URL - not which
/// host/CDN serves it (a picsum.photos placeholder and a real CDN asset are equally valid here).
/// </summary>
public readonly record struct ImageUrl
{
    public string Value { get; }

    private ImageUrl(string value) => Value = value;

    public static ImageUrl Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"'{value}' is not a valid absolute http(s) image URL.", nameof(value));
        }

        return new ImageUrl(value);
    }

    public static implicit operator string(ImageUrl imageUrl) => imageUrl.Value;

    public override string ToString() => Value;
}
