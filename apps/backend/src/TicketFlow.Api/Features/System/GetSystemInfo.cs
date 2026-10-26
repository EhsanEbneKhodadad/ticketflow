using Microsoft.AspNetCore.Http.HttpResults;

namespace TicketFlow.Api.Features.System;

public sealed record SystemInfo(string Name, string Version, string Environment);

public static class GetSystemInfo
{
    public static void MapSystemInfo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/system/info", (IHostEnvironment environment) =>
            TypedResults.Ok(new SystemInfo("TicketFlow", "0.1.0", environment.EnvironmentName)))
            .WithName("GetSystemInfo")
            .WithTags("System")
            .WithSummary("Returns application identity and release information.");
    }
}
