namespace PiiGateway.Core.Domain.Enums;

public enum ReviewStatus
{
    Pending,
    Confirmed,
    Rejected, // Unused — kept to preserve DB ordinal values
    AddedManual
}
