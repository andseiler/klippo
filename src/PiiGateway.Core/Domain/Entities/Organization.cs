namespace PiiGateway.Core.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string? LlmProvider { get; set; }
    public string? Settings { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
