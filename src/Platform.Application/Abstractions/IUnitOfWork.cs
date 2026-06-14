namespace Platform.Application.Abstractions;

/// <summary>Commits pending changes made through the repositories as one unit.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
