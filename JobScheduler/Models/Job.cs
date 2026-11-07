namespace JobScheduler.Models;

public class Job
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime NextRunTime { get; set; }
    public int? IntervalSeconds { get; set; }   

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }

    public DateTime? LastRunTime { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   
}