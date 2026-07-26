namespace Kart.Product.Application.Common.Interfaces;

/// <summary>
/// ddd-model.md's <c>CanWrite</c> invariant: a coarse role-claim check only (Admin or Partner
/// API client identity) - no ownership dimension, no persisted grant table. <see cref="ClientId"/>
/// is recorded as the <c>created_by</c>/<c>updated_by</c> audit value (BRD §24.3) - the Admin/
/// Partner API client identity that performed the write, never an end customer.
/// </summary>
public interface ICurrentPrincipal
{
    string ClientId { get; }

    bool IsAdminOrPartner { get; }
}
