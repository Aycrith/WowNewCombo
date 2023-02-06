using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;

namespace BlazorServer;

public static class Program
{
    private const string hostUrl = "http://0.0.0.0:5000";

    public static void Main(string[] args)
    {
        while (true)
        {
            Log.Information("Program.Main(): Starting blazor server");
            try
            {
                IHost host = CreateHostBuilder(args).Build();
                var logger = host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();

                AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs args) =>
                {
                    Exception e = (Exception)args.ExceptionObject;
                    logger.LogError(e, e.Message);
                };

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Information($"Program.Main(): {ex.Message}");
                Log.Information("");
                System.Threading.Thread.Sleep(3000);
            }
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>

        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(hostUrl);
                webBuilder.UseStartup<Startup>();
            })
        .ConfigureLogging((hostingContext, logging) =>
        {
            logging.AddConsole();
            logging.AddEventSourceLogger();
        });
}