using System.Text.Json;
using JobScheduler.Models;

namespace JobScheduler.Dtos;

public record JobResponse(
    Guid Id,
    string Name,
    string Type,
    JsonElement Payload,
    string Status,
    DateTime NextRunTime,
    int? IntervalSeconds,
    int RetryCount,
    int MaxRetries,
    DateTime? LastRunTime,
    string? LastError,
    DateTime CreatedAt)
{
    public static JobResponse FromEntity(Job job) => new(
        job.Id,
        job.Name,
        job.Type,
        JsonSerializer.Deserialize<JsonElement>(job.Payload),
        job.Status.ToString(),
        AsUtc(job.NextRunTime),
        job.IntervalSeconds,
        job.RetryCount,
        job.MaxRetries,
        AsUtcOrNull(job.LastRunTime),
        job.LastError,
        AsUtc(job.CreatedAt));

    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtcOrNull(DateTime? value) =>
        value.HasValue ? AsUtc(value.Value) : null;
}