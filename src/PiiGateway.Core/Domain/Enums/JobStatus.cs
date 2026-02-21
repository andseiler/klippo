namespace PiiGateway.Core.Domain.Enums;

public enum JobStatus
{
    Created,
    Processing,
    ReadyReview,
    InReview,
    Pseudonymized,
    ScanPassed,
    ScanFailed,
    DePseudonymized,
    Failed,
    Cancelled
}
