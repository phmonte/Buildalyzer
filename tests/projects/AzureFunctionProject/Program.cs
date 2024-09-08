using Microsoft.Extensions.Hosting;

namespace AzureFunctionProject;

public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureServices(_ =>
            {})
            .Build();

        host.Run();
    }
}