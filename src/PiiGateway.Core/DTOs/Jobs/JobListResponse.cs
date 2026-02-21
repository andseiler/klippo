namespace PiiGateway.Core.DTOs.Jobs;

public class JobListResponse
{
    public IReadOnlyList<JobResponse> Items { get; set; } = Array.Empty<JobResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
