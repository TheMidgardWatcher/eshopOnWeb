namespace Microsoft.eShopWeb.ApplicationCore;

public class DeliveryProcessorServiceConfiguration
{
    public static string ConfigSection => "DeliveryProcessorService";

    public string Url { get; set; }
    public bool IsEnabled { get; set; }
}
