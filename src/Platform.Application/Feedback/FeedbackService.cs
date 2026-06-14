using Platform.Application.Abstractions;
using Platform.Domain.Feedback;

namespace Platform.Application.Feedback;

public sealed class FeedbackService
{
    private readonly IFeedbackRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public FeedbackService(IFeedbackRepository repository, IUnitOfWork unitOfWork, IIdGenerator ids, IClock clock)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _ids = ids;
        _clock = clock;
    }

    /// <summary>Records a piece of feedback. Returns the created entry.</summary>
    public async Task<FeedbackEntry> SubmitAsync(string? email, string message, CancellationToken ct = default)
    {
        var entry = FeedbackEntry.Create(_ids.NewId("fb"), email, message, _clock.UtcNow);
        await _repository.AddAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return entry;
    }

    public Task<IReadOnlyList<FeedbackEntry>> ListAsync(int limit = 100, CancellationToken ct = default)
        => _repository.ListAsync(limit, ct);
}
