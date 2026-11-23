using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus.Serialization;

/// <summary>
/// Єдині налаштування JSON-серіалізації (camelCase, enum як рядки, нечутливість до регістру)
/// для всіх publisher-ів і consumer-ів мікросервісної платформи.
/// </summary>
public static class EventBusJsonOptions
{
    public static readonly JsonSerializerOptions Default = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
