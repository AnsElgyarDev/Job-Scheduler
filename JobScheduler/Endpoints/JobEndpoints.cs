using System.Text.Json;
using JobScheduler.Data;
using JobScheduler.Dtos;
using JobScheduler.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobScheduler.Endpoints;

public static class JobEndpoints
{
    private const int MaxNameLength = 200;
    private const int MinIntervalSeconds = 10;
    private const int DefaultMaxRetries = 2;
    private const int MaxRetriesLimit = 10;
    private const int DefaultListLimit = 50;
    private const int MaxListLimit = 200;

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/jobs").WithTags("Jobs");

        group.MapPost("/", CreateJob);
        group.MapGet("/", ListJobs);
        group.MapGet("/{id:guid}", GetJob);
        group.MapDelete("/{id:guid}", CancelJob);

        return app;
    }

    private static async Task<Results<Created<JobResponse>, ValidationProblem>> CreateJob(
        CreateJobRequest request, AppDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var name = request.Name?.Trim();
        var type = request.Type?.Trim().ToLowerInvariant();

        var errors = Validate(request, name, type, now);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = name!,
            Type = type!,
            Payload = request.Payload.GetRawText(),
            NextRunTime = (request.RunAt ?? now).UtcDateTime,
            IntervalSeconds = request.IntervalSeconds,
            Status = JobStatus.Pending,
            MaxRetries = request.MaxRetries ?? DefaultMaxRetries,
            CreatedAt = now.UtcDateTime
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/jobs/{job.Id}", JobResponse.FromEntity(job));
    }

    private static async Task<Results<Ok<List<JobResponse>>, ValidationProblem>> ListJobs(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] JobStatus? status = null,
        [FromQuery] int? limit = null)
    {
        var pageSize = limit ?? DefaultListLimit;
        if (pageSize is < 1 or > MaxListLimit)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["limit"] = new[] { $"Limit must be between 1 and {MaxListLimit}." }
            });
        }

        var query = db.Jobs.AsNoTracking();

        if (status is { } s)
        {
            query = query.Where(j => j.Status == s);
        }

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(pageSize)
            .ToListAsync(ct);

        return TypedResults.Ok(jobs.Select(JobResponse.FromEntity).ToList());
    }

    private static async Task<Results<Ok<JobResponse>, NotFound>> GetJob(
        Guid id, AppDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);

        return job is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(JobResponse.FromEntity(job));
    }

    private static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> CancelJob(
        Guid id, AppDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (job.Status != JobStatus.Pending)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Job cannot be cancelled",
                Detail = $"Only Pending jobs can be cancelled. This job is {job.Status}."
            });
        }

        job.Status = JobStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }


    private static Dictionary<string, string[]> Validate(
        CreateJobRequest request, string? name, string? type, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Length > MaxNameLength)
            errors["name"] = new[] { $"Name must be {MaxNameLength} characters or fewer." };

        if (string.IsNullOrEmpty(type))
            errors["type"] = new[] { "Type is required." };
        else if (type != "http")
            errors["type"] = new[] { "Only the 'http' type is supported." };

        if (request.Payload.ValueKind != JsonValueKind.Object)
            errors["payload"] = new[] { "Payload is required and must be a JSON object." };
        else if (type == "http")
            ValidateHttpPayload(request.Payload, errors);

        if (request.RunAt is { } runAt && runAt < now)
            errors["runAt"] = new[] { "Run time must not be in the past." };

        if (request.IntervalSeconds is < MinIntervalSeconds)
            errors["intervalSeconds"] = new[] { $"Interval must be at least {MinIntervalSeconds} seconds." };

        if (request.MaxRetries is < 0 or > MaxRetriesLimit)
            errors["maxRetries"] = new[] { $"Max retries must be between 0 and {MaxRetriesLimit}." };

        return errors;
    }

    private static void ValidateHttpPayload(JsonElement payload, Dictionary<string, string[]> errors)
    {
        var urlIsValid =
            payload.TryGetProperty("url", out var urlElement)
            && urlElement.ValueKind == JsonValueKind.String
            && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        if (!urlIsValid)
            errors["payload.url"] = new[] { "Must be an absolute http or https URL." };

        if (payload.TryGetProperty("method", out var methodElement))
        {
            var method = methodElement.ValueKind == JsonValueKind.String
                ? methodElement.GetString()
                : null;

            var methodIsValid =
                string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                || string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

            if (!methodIsValid)
                errors["payload.method"] = new[] { "Must be GET or POST." };
        }
    }
}