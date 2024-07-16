using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace StrykerReplication
{
    public class TestFunction
    {
        [FunctionName("Function1")]
        public async Task Run([CosmosDBTrigger(
            databaseName: "databaseName",
            collectionName: "collectionName",
            ConnectionStringSetting = "",
            LeaseCollectionName = "leases")]string input,
            ILogger log)
        {
            if (input != null)
            {
                log.LogInformation("Document modified");
            }
        }
    }
}
