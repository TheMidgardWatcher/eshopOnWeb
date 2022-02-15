using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OrderItemsReserver;

[assembly: FunctionsStartup(typeof(Startup))]

namespace OrderItemsReserver;

public sealed class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddHttpClient();
    }
}
