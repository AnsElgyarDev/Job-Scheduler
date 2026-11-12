using System.Text.Json;

namespace JobScheduler.Dtos;

public record CreateJobRequest(
    string? Name,
    string? Type,
    JsonElement Payload,
    DateTimeOffset? RunAt,
    int? IntervalSeconds,
    int? MaxRetries);