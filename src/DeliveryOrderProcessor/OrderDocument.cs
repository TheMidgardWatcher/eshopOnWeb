using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Newtonsoft.Json;

namespace DeliveryOrderProcessor;

public  class OrderDocument
{
    [JsonProperty("id")]
    public string Id { get; set; }
    [JsonProperty("buyerId")]
    public string BuyerId { get; set; }
    [JsonProperty("shipToAddress")]
    public Address ShipToAddress { get; set; }
    [JsonProperty("orderItems")]
    public List<OrderItem> OrderItems { get; set; }
}
