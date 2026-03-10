using Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeadlessServer;

public sealed class HeadlessRouteControlHost : IAsyncDisposable
{
    private readonly ILogger<HeadlessRouteControlHost> logger;
    private readonly IBotRouteControlService routeControl;
    private readonly HeadlessRouteControlOptions options;

    private WebApplication? app;

    public Uri? BaseAddress { get; private set; }

    public HeadlessRouteControlHost(
        ILogger<HeadlessRouteControlHost> logger,
        IBotRouteControlService routeControl,
        HeadlessRouteControlOptions options)
    {
        this.logger = logger;
        this.routeControl = routeControl;
        this.options = options;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("[HeadlessRouteCtl ] Loopback route-control host disabled.");
            return false;
        }

        if (app != null)
        {
            return true;
        }

        WebApplication? localApp = null;
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(Array.Empty<string>());
            builder.WebHost.UseUrls($"http://{options.Host}:{options.Port}");
            builder.Services.AddSingleton(routeControl);

            localApp = builder.Build();
            MapEndpoints(localApp);

            await localApp.StartAsync(cancellationToken).ConfigureAwait(false);

            string? address = localApp.Urls.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(address))
            {
                IServerAddressesFeature? feature = localApp.Services
                    .GetRequiredService<IServer>()
                    .Features
                    .Get<IServerAddressesFeature>();
                address = feature?.Addresses.FirstOrDefault();
            }

            app = localApp;
            BaseAddress = string.IsNullOrWhiteSpace(address) ? null : new Uri(address);

            logger.LogInformation(
                "[HeadlessRouteCtl ] Loopback route-control host listening on {Address}",
                BaseAddress?.ToString() ?? "<unknown>");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HeadlessRouteCtl ] Failed to start loopback route-control host.");
            if (localApp != null)
            {
                await localApp.DisposeAsync().ConfigureAwait(false);
            }

            BaseAddress = null;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (app == null)
        {
            return;
        }

        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            await app.StopAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[HeadlessRouteCtl ] StopAsync failed during disposal.");
        }

        await app.DisposeAsync().ConfigureAwait(false);
        app = null;
        BaseAddress = null;
    }

    private void MapEndpoints(WebApplication localApp)
    {
        localApp.MapGet("/api/bot/route/state", (HttpContext context, IBotRouteControlService routeControlService) =>
        {
            string correlationId = EnsureCorrelationId(context);
            try
            {
                return Results.Ok(routeControlService.GetState());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[HeadlessRouteCtl ] Get route state failed.");
                return Results.Json(new { Error = ex.Message, CorrelationId = correlationId }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        localApp.MapPost("/api/bot/route/apply", async (HttpContext context, IBotRouteControlService routeControlService) =>
        {
            string correlationId = EnsureCorrelationId(context);
            try
            {
                BotRouteCommandRequest? request = await context.Request
                    .ReadFromJsonAsync<BotRouteCommandRequest>(cancellationToken: context.RequestAborted)
                    .ConfigureAwait(false);

                if (request == null)
                {
                    return Results.BadRequest(new { Error = "Request body is required.", CorrelationId = correlationId });
                }

                if (!request.ClearOverride && string.IsNullOrWhiteSpace(request.FileName))
                {
                    return Results.BadRequest(new { Error = "FileName is required when ClearOverride is false.", CorrelationId = correlationId });
                }

                BotRouteCommandResult result = routeControlService.Apply(request);
                return result.Success ? Results.Ok(result) : Results.Conflict(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[HeadlessRouteCtl ] Apply route failed.");
                return Results.Json(new { Error = ex.Message, CorrelationId = correlationId }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }

    private static string EnsureCorrelationId(HttpContext context)
    {
        string correlationId = string.IsNullOrWhiteSpace(context.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : context.TraceIdentifier;

        context.Response.Headers["X-Correlation-ID"] = correlationId;
        return correlationId;
    }
}
