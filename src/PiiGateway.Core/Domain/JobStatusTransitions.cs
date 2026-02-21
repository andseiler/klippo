using PiiGateway.Core.Domain.Enums;

namespace PiiGateway.Core.Domain;

public static class JobStatusTransitions
{
    private static readonly Dictionary<JobStatus, HashSet<JobStatus>> AllowedTransitions = new()
    {
        [JobStatus.Created] = new() { JobStatus.Processing, JobStatus.Cancelled },
        [JobStatus.Processing] = new() { JobStatus.ReadyReview, JobStatus.Failed, JobStatus.Cancelled },
        [JobStatus.ReadyReview] = new() { JobStatus.InReview },
        [JobStatus.InReview] = new() { JobStatus.Pseudonymized, JobStatus.ReadyReview },
        [JobStatus.Pseudonymized] = new() { JobStatus.InReview, JobStatus.DePseudonymized },
        [JobStatus.DePseudonymized] = new() { JobStatus.DePseudonymized },
    };

    public static void Validate(JobStatus current, JobStatus next)
    {
        if (!AllowedTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
        {
            throw new InvalidOperationException(
                $"Invalid job status transition from '{current}' to '{next}'.");
        }
    }

    public static bool IsValid(JobStatus current, JobStatus next)
    {
        return AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
    }
}
