using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeliveryOrderProcessor;

public static class DeliveryOrderProcessorService
{
    [FunctionName("DeliveryOrderProcessorService")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
        ExecutionContext ctx,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            JObject data = JsonConvert.DeserializeObject(requestBody) as JObject;
            var order = new OrderDocument
            {
                Id = Guid.NewGuid().ToString(),
                BuyerId = data["BuyerId"].Value<string>(),
                ShipToAddress = data["ShipToAddress"].ToString().FromJson<Address>(),
                OrderItems    = data["OrderItems"].ToString().FromJson<List<OrderItem>>()
            };
            
            var config = new ConfigurationBuilder()
                .SetBasePath(ctx.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            // Get the connection string from app settings and use it to create a connection.
            var str = config["ConnectionStrings__DeliveryOrderProcessorDbConnection"] ??
                      config["ConnectionStrings:DeliveryOrderProcessorDbConnection"];


            var documentClient = new CosmosClient(str);

            var db = await documentClient.CreateDatabaseIfNotExistsAsync("EshopDeliveries");
            var containerResponse = await db.Database.CreateContainerIfNotExistsAsync(new ContainerProperties("Orders", "/buyerId"));
            var itemResponse = await containerResponse.Container.UpsertItemAsync(order, new PartitionKey(order.BuyerId));

            #region old code
            //await using (SqlConnection conn = new SqlConnection(str))
            //{
            //    conn.Open();
            //    var text = @"IF  NOT EXISTS (SELECT * FROM sys.objects 
            //            WHERE object_id = OBJECT_ID(N'[dbo].[DeliveryOrders]') AND type in (N'U'))

            //            BEGIN
            //            CREATE TABLE [dbo].[DeliveryOrders](
            //                Id BIGINT PRIMARY KEY IDENTITY(1,1),
            //                BuyerId NVARCHAR(256) NOT NULL,
            //                Address NVARCHAR(MAX) NOT NULL,
            //                Items NVARCHAR(MAX) NOT NULL,
            //                Price DECIMAL(9,2) NOT NULL
            //            )
            //            END";

            //    await using (SqlCommand cmd = new SqlCommand(text, conn))
            //    {
            //        // Execute the command and log the # rows affected.
            //        var rows = await cmd.ExecuteNonQueryAsync();
            //        log.LogInformation($"{rows} rows were updated");
            //    }

            //    text = $@"INSERT INTO [dbo].[DeliveryOrders](BuyerId, Address, Items, Price)
            //        VALUES (N'{order.BuyerId}', N'{order.ShipToAddress.ToJson()}', N'{order.OrderItems.ToJson()}', {order.OrderItems.Sum(x => x.Units * x.UnitPrice)})";

            //    await using (SqlCommand cmd = new SqlCommand(text, conn))
            //    {
            //        // Execute the command and log the # rows affected.w
            //        var rows = await cmd.ExecuteNonQueryAsync();
            //        message = $"{rows} deliveries were placed";
            //        log.LogInformation(message);
            //    }
            //} 
            #endregion

            return itemResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created ? new OkResult(): new BadRequestResult();
        }
        catch (CosmosException e)
        {
            log.LogError(e, $"Exception occurred while running function:\r\n{e:ToString}");
            throw;
        }
    }
}
