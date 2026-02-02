using System.Text.Json.Serialization;

namespace TerraformApi.Models;

public class TerraformOutput
{
    [JsonPropertyName("broker_arn")]
    public OutputValue<string>? BrokerArn { get; set; }

    [JsonPropertyName("web_console_url")]
    public OutputValue<string>? WebConsoleUrl { get; set; }

    [JsonPropertyName("openwire_ssl_active_url")]
    public OutputValue<string>? OpenwireSslActiveUrl { get; set; }

    [JsonPropertyName("openwire_ssl_standby_url")]
    public OutputValue<string>? OpenwireSslStandbyUrl { get; set; }

    [JsonPropertyName("openwire_ssl_urls")]
    public OutputValue<List<string>>? OpenwireSslUrls { get; set; }
}

public class OutputValue<T>
{
    [JsonPropertyName("value")]
    public T? Value { get; set; }

    [JsonPropertyName("type")]
    public object? Type { get; set; }
}
