using System.ComponentModel.DataAnnotations;

namespace PiiGateway.Core.DTOs.Jobs;

public class CreateJobRequest
{
    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
