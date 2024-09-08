using Gensokyo.Yukari.Services;
using Microsoft.AspNetCore.WebSockets;

namespace Gensokyo.Yukari;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<WorkerManager>();
        builder.Services.AddControllers();
        builder.Services.AddLogging();
        builder.Services.AddWebSockets(settings =>
        {
            settings.AllowedOrigins.Add("https://localhost:7179");
            settings.KeepAliveInterval = TimeSpan.FromSeconds(30);
        });
        var app = builder.Build();
        app.UseHttpsRedirection();
        app.MapControllers();
        app.UseWebSockets();
        app.Run();
    }
}