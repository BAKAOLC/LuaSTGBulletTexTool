using System.Text.Json.Serialization;

namespace TexCombineTool.Models
{
    internal record CenterData(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y);

    internal record SpriteData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("texture")]
        string Texture,
        [property: JsonPropertyName("rect")] RectData Rect,
        [property: JsonPropertyName("center")] CenterData? Center,
        [property: JsonPropertyName("scaling")]
        double? Scaling,
        [property: JsonPropertyName("blend")] string? Blend);

    internal record SpriteSequenceData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("sprites")]
        List<string> Sprites,
        [property: JsonPropertyName("interval")]
        int Interval,
        [property: JsonPropertyName("blend")] string? Blend);
}