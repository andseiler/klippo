namespace PiiGateway.Core.Domain.Enums;

public enum ActionType
{
    PiiDetected,
    PiiConfirmed,
    PiiRejected,
    PiiAddedManual,
    DocumentPseudonymized,
    SecondScanPassed,
    SecondScanFailed,
    TextCopied,
    ResponseDepseudonymized,
    ReviewerTrainingCompleted,
    DpiaTriggerShown,
    DpiaAcknowledged,
    UserRegistered,
    UserLogin,
    JobCreated,
    JobStatusChanged,
    JobDeleted,
    LlmScanCompleted
}
