using Platform.Domain.Feedback;

namespace Platform.Application.Feedback;

public interface IFeedbackRepository
{
    Task AddAsync(FeedbackEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<FeedbackEntry>> ListAsync(int limit, CancellationToken ct = default);
}
