using System.IO;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Frontend;

public static class DependencyInjection
{
    public static IServiceCollection AddFrontend(this IServiceCollection services)
    {
        services.AddBlazorBootstrap();

        // Admin/runtime settings persisted to runtime_feature_flags.json
        services.AddSingleton<MountUnlockAdminService>();
        services.AddSingleton<PathingAdminService>();
        services.AddSingleton<HumanizationAdminService>();
        services.AddSingleton<CombatRotationAdminService>();

        services.AddScoped<Services.HazardHeatMapService>();

        // Event aggregation service for real-time monitoring and LLM integration
        services.AddSingleton<Services.EventAggregationService>();
        services.AddHostedService(sp => sp.GetRequiredService<Services.EventAggregationService>());

        services.AddRazorPages();

        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        return services;
    }

    public static IApplicationBuilder UseCustomStaticFiles(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        DataConfig dataConfig = app.ApplicationServices.GetRequiredService<DataConfig>();

        // Helper to safely create static file provider
        void AddStaticFiles(string path, string requestPath)
        {
            var fullPath = Path.Combine(env.ContentRootPath, path);
            if (Directory.Exists(fullPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(fullPath),
                    RequestPath = requestPath
                });
            }
        }

        AddStaticFiles(dataConfig.Path, "/path");
        AddStaticFiles(dataConfig.Leaflet, "/tiles");
        AddStaticFiles(dataConfig.ExpDbc, "/dbc");
        AddStaticFiles(dataConfig.ExpArea, "/area");
        AddStaticFiles(dataConfig.NpcSpawnLocations, "/npcspawnlocations");
        AddStaticFiles(dataConfig.MailboxLocations, "/mailboxlocations");

        return app;
    }
}
