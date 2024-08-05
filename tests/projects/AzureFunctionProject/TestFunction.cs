using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureFunctionProject;

public class TestFunction
{
    [FunctionName(nameof(TestFunction))]
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