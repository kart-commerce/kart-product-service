using Kart.Product.Application.Common.Interfaces;

namespace Kart.Product.IntegrationTests.Fakes;

public sealed class FixedCurrentPrincipal(string clientId, bool isAdminOrPartner = true) : ICurrentPrincipal
{
    public string ClientId { get; } = clientId;

    public bool IsAdminOrPartner { get; } = isAdminOrPartner;
}
