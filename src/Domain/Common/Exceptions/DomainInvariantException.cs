namespace Kart.Product.Domain.Common.Exceptions;

/// <summary>
/// Base for exceptions raised by domain entity methods guarding an invariant (e.g. "already
/// archived", "already discontinued") - as opposed to Application-layer concerns like "not found"
/// or "forbidden", which live in Kart.Product.Application.Common.Exceptions since Domain must
/// never depend on Application.
/// </summary>
public abstract class DomainInvariantException(string message) : Exception(message);
