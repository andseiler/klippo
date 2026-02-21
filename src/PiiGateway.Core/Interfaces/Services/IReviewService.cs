using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.DTOs.Review;

namespace PiiGateway.Core.Interfaces.Services;

public interface IReviewService
{
    Task<ReviewDataResponse> GetReviewDataAsync(Guid jobId);
    Task UpdateEntityAsync(Guid jobId, Guid entityId, UpdateEntityRequest request, Guid userId, string? ipAddress);
    Task DeleteEntityAsync(Guid jobId, Guid entityId, Guid userId, string? ipAddress);
    Task DeleteAllEntitiesAsync(Guid jobId, Guid userId, string? ipAddress);
    Task<PiiEntity> AddManualEntityAsync(Guid jobId, AddEntityRequest request, Guid userId, string? ipAddress);
    Task CompleteReviewAsync(Guid jobId, Guid userId, string? ipAddress);
    Task ReopenReviewAsync(Guid jobId, Guid userId, string? ipAddress);
    Task UpdateSegmentTextAsync(Guid jobId, Guid segmentId, UpdateSegmentRequest request, Guid userId, string? ipAddress);
    Task UpdatePseudonymizedTextAsync(Guid jobId, UpdatePseudonymizedTextRequest request, Guid userId, string? ipAddress);
}
