using MediatR;

namespace Kart.Product.Application.Features.ProjectCategoryEvent;

/// <summary>Category &amp; Attribute Management (Admin) flow: project an incoming CategoryUpdated event onto product_read_model.</summary>
public sealed record ProjectCategoryEventCommand(string EventType, string PayloadJson) : IRequest;
