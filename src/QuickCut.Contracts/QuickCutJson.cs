using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickCut.Contracts;

public static class QuickCutJson
{
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }
}
