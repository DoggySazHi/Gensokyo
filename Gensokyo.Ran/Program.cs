namespace Gensokyo.Ran;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(RanConfig.Load());
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}