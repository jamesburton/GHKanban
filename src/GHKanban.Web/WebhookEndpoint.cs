using GHKanban.Config;
using GHKanban.GitHub;
using GHKanban.Sync;

namespace GHKanban.Web;

public static class WebhookEndpoint
{
    public static void MapWebhook(this WebApplication app)
    {
        app.MapPost("/hook", async (
            HttpContext ctx,
            ConfigStore configStore,
            WebhookEventProcessor processor,
            ILogger<WebhookEndpointMarker> log) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();

            var snap = configStore.Current;
            var secretEnv = snap.GitHub.Webhook.SecretEnv;
            if (secretEnv is not null)
            {
                var secret = Environment.GetEnvironmentVariable(secretEnv) ?? "";
                var sig = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
                if (!WebhookSignatureValidator.Validate(secret, body, sig))
                {
                    log.LogWarning("Webhook signature validation failed");
                    return Results.Unauthorized();
                }
            }

            var eventName = ctx.Request.Headers["X-GitHub-Event"].ToString();
            var ev = EventMapper.MapIssueEvent(eventName, body);
            if (ev is null)
            {
                log.LogInformation("Webhook event {Event} not mapped; ignored", eventName);
                return Results.Ok();
            }

            // Single dispatch path: write to channel; the WebhookEventProcessor consumer updates
            // the store AND invokes OnEvent (wired in Program.cs to the agent dispatcher).
            await processor.Writer.WriteAsync(ev, ctx.RequestAborted);
            return Results.Ok();
        });
    }
}

// Marker type used solely as the ILogger<T> category for the webhook endpoint handler.
internal sealed class WebhookEndpointMarker;
