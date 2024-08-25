namespace Gensokyo.Ran;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddSingleton(RanConfig.Load());

        var host = builder.Build();
        host.Run();
    }
}