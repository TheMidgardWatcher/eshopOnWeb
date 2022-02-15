using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace OrderItemsReserver;

public class OrderItemsReserverService
{
    private readonly IHttpClientFactory _httpFactory;

    public OrderItemsReserverService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    [FunctionName("OrderItemsReserverService")]
    public async Task Run([ServiceBusTrigger("orders-reservations", Connection = "AzureServiceBusConnection")]
        string queueItem,
        ExecutionContext ctx,
        ILogger log)
    {
        log.LogInformation($"{nameof(OrderItemsReserverService)} is triggered by http call.");

        //string requestBody = await new StreamReader(queueItem).ReadToEndAsync();
        //dynamic data = JsonConvert.DeserializeObject(requestBody);
        dynamic data = JsonConvert.DeserializeObject(queueItem);

        if (data == null)
            throw new InvalidOperationException("Request body should contain order info");

        var config = new ConfigurationBuilder()
            .SetBasePath(ctx.FunctionAppDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        try
        {
            string connectionString = config["OrdersReservationStorage"] ?? config["Values:OrdersReservationStorage"];//"DummyConnectionString";

            var options = new BlobClientOptions { Retry = { MaxRetries = 3 } };
            var storageClient = new BlobServiceClient(connectionString, options);
            var blobContainer = storageClient.GetBlobContainerClient("orders-container");
            await blobContainer.CreateIfNotExistsAsync();

            //var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(queueItem));

            var blobName = $"OrderReservation_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";
            var response = await blobContainer.UploadBlobAsync(blobName, stream);

            log.LogInformation($"Order reservation was successfully uploaded to blob: '{blobName}'.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while processing a function.");

            //put Logic app call here
            var emailSenderAppUrl = config["FailoverLogicAppUrl"] ?? config["Values:FailoverLogicAppUrl"];
            var request = new HttpRequestMessage(HttpMethod.Post, emailSenderAppUrl)
            {
                Content = new StringContent(queueItem, Encoding.UTF8, "application/json")
            };

            var client = _httpFactory.CreateClient();
            await client.SendAsync(request);
        }
    }
}
