using Kart.Product.Application.Common.Interfaces;

namespace Kart.Product.ContractTests.Fakes;

/// <summary>The in-memory fake repositories persist synchronously on <c>Add</c>/mutation - there
/// is no real transaction to commit in the contract-test host.</summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
