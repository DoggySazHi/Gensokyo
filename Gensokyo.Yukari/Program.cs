using Gensokyo.Yukari.Services;

namespace Gensokyo.Yukari;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<WorkerManager>();
        var app = builder.Build();
        app.UseHttpsRedirection();
        app.Run();
    }
}