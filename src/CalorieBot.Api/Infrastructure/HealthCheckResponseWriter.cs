using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CalorieBot.Api.Infrastructure;

/// <summary>
/// Отдаю результат health check как JSON — так его удобно читать и человеку, и мониторингу.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message,
                data = entry.Value.Data
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
