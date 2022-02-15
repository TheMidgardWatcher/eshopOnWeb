using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptions<DeliveryProcessorServiceConfiguration> _deliveryProcessorServiceOptions;
    private readonly ConfigurationManager _config;
    private readonly IAppLogger<OrderService> _logger;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IHttpClientFactory httpFactory,
        IOptions<DeliveryProcessorServiceConfiguration> deliveryProcessorServiceOptions,
        ConfigurationManager config,
        IAppLogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _httpFactory = httpFactory;
        _deliveryProcessorServiceOptions = deliveryProcessorServiceOptions;
        _config = config;
        _logger = logger;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification =
            new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name,
                _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        order = await _orderRepository.AddAsync(order);

        if (_deliveryProcessorServiceOptions.Value?.IsEnabled == true)
        {
            var httpClient = _httpFactory.CreateClient();
            var deliveryRequest =
                new HttpRequestMessage(HttpMethod.Post, _deliveryProcessorServiceOptions.Value.Url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(order,
                        new JsonSerializerOptions { WriteIndented = true }))
                };

            _logger.LogInformation(
                $"Sending order information to delivery service. ServiceUrl: '{_deliveryProcessorServiceOptions.Value?.Url}'");
            var result = await httpClient.SendAsync(deliveryRequest);
            if (result.IsSuccessStatusCode)
            {
                _logger.LogInformation("Order information was successfully sent to delivery service.");
            }
            else
            {
                _logger.LogWarning(
                    $"Order information wasn't sent to delivery service. ResponseStatus: '{result.StatusCode}'.\r\nMessage: {await result.Content.ReadAsStringAsync()}");
            }
        }
        else
        {
            _logger.LogWarning(
                $"DeliveryOrderProcessorService was not called. IsEnabled: '{_deliveryProcessorServiceOptions.Value?.IsEnabled}'");
        }

        var connectionString = _config["AzureServiceBusConnection"] ?? _config["AzureServiceBusConnection"];
        var queueName = _config["AzureServiceBusQueue"] ?? _config["AzureServiceBusQueue"];

        await using var client = new ServiceBusClient(connectionString);
        ServiceBusSender sender = client.CreateSender(queueName);
        var message = new ServiceBusMessage(JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = true }));
        await sender.SendMessageAsync(message);

    }
}
