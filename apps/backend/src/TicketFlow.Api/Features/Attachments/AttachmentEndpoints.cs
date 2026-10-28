using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Security;
using TicketFlow.Application.Attachments;

namespace TicketFlow.Api.Features.Attachments;

public sealed class UploadAttachmentForm
{
    public required IFormFile File { get; init; }
    public int ExpectedVersion { get; init; }
    public Guid? MessageId { get; init; }
}

public static class AttachmentEndpoints
{
    public const long MaxRequestBytes = AttachmentPolicy.MaxFileBytes + 65536;

    public static void UseAttachmentBodyLimit(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Runs before CSRF/form parsing; the rest of the API retains its 64 KiB limit.
            if (HttpMethods.IsPost(context.Request.Method)
                && context.Request.Path.StartsWithSegments("/api/v1/tickets", StringComparison.OrdinalIgnoreCase)
                && context.Request.Path.Value!.TrimEnd('/').EndsWith("/attachments", StringComparison.OrdinalIgnoreCase))
            {
                var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (size is { IsReadOnly: false }) size.MaxRequestBodySize = MaxRequestBytes;
                if (context.Request.ContentLength > MaxRequestBytes)
                    throw new RuleException(ErrorKind.Validation, "Upload one file up to 10 MiB.");
                context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions
                {
                    MultipartBodyLengthLimit = MaxRequestBytes,
                    ValueLengthLimit = 128,
                    ValueCountLimit = 8,
                    MultipartBoundaryLengthLimit = 128,
                    MultipartHeadersCountLimit = 16,
                    MultipartHeadersLengthLimit = 4096,
                    MemoryBufferThreshold = 65536
                }));
            }
            await next(context);
        });
    }

    public static void MapAttachments(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/tickets/{id:guid}/attachments").RequireAuthorization().WithTags("Attachments");
        api.MapGet("", async (Guid id, AttachmentWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.ListAsync(RequestIdentity.Actor(context), id, ct)));
        api.MapPost("", async (Guid id, [FromForm] UploadAttachmentForm input, AttachmentWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            if (input.File is null || input.ExpectedVersion < 1 || context.Request.Form.Files.Count != 1
                || input.File.Length is <= 0 or > AttachmentPolicy.MaxFileBytes)
                throw new RuleException(ErrorKind.Validation, "Provide one nonempty file up to 10 MiB and the current ticket version.");
            using var stream = input.File.OpenReadStream();
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, ct);
            var attachment = await workflow.UploadAsync(RequestIdentity.Actor(context), id, input.File.FileName,
                output.ToArray(), input.ExpectedVersion, input.MessageId, ct);
            return TypedResults.Created($"/api/v1/tickets/{id}/attachments/{attachment.Id}/download", attachment);
        }).DisableAntiforgery(); // The existing same-origin header CSRF middleware validates every write before model binding.
        api.MapGet("/{attachmentId:guid}/download", async (Guid id, Guid attachmentId, AttachmentWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            var download = await workflow.DownloadAsync(RequestIdentity.Actor(context), id, attachmentId, ct);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "private, no-store";
            return TypedResults.File(download.Bytes, download.ContentType, download.FileName, enableRangeProcessing: false);
        });
    }
}
