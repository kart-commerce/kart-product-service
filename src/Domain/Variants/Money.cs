namespace Kart.Product.Domain.Variants;

/// <summary>api-contract.yaml's <c>Money</c> schema - a Variant's base/list price (ddd-model.md:
/// price lives at the Variant/SKU level, never on the parent ProductGroup). Protects the two
/// invariants every caller of this type relies on without ever restating them: a price is never
/// negative, and a currency is always a well-formed 3-letter ISO 4217 code, normalized so "usd"
/// and "USD" are never treated as two different currencies.</summary>
public sealed record Money
{
    private const int CurrencyCodeLength = 3;

    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money amount cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != CurrencyCodeLength || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"'{currency}' is not a valid 3-letter ISO 4217 currency code.", nameof(currency));
        }

        Amount = amount;
        Currency = normalized;
    }
}
