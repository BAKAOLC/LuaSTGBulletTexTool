using Newtonsoft.Json;

namespace TexCombineTool.Models
{
    internal record CenterData(
        [property: JsonProperty("x")] double X,
        [property: JsonProperty("y")] double Y);

    internal record SpriteData(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("texture")] string Texture,
        [property: JsonProperty("rect")] RectData Rect,
        [property: JsonProperty("center")] CenterData? Center,
        [property: JsonProperty("scaling")] double? Scaling,
        [property: JsonProperty("blend")] string? Blend);

    internal record SpriteSequenceData(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("sprites")] List<string> Sprites,
        [property: JsonProperty("interval")] int Interval,
        [property: JsonProperty("blend")] string? Blend);
}