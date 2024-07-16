using Microsoft.Extensions.Hosting;

namespace StrykerReplication
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureServices(s =>
                {

                })
                .Build();

            host.Run();

        }
    }
}