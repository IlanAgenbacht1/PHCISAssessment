using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phcis.Booking.Shared;

public static class SharedJson
{
    /// <summary>Web defaults plus enums as strings. The API and client must agree on this.</summary>
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
