using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace TexCombineTool
{
    internal record TextureData(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("path")] string Path,
        [property: JsonProperty("mipmap")] bool Mipmap);

    internal record RectData(
        [property: JsonProperty("x")] int X,
        [property: JsonProperty("y")] int Y,
        [property: JsonProperty("width")] int Width,
        [property: JsonProperty("height")] int Height);

    internal static class RectExtensions
    {
        public static Rectangle ToRectangle(this RectData rectData)
        {
            return new(rectData.X, rectData.Y, rectData.Width, rectData.Height);
        }

        public static RectData ToRectData(this Rectangle rectangle)
        {
            return new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
    }


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

    internal record VariantData(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("sprite")] string? Sprite,
        [property: JsonProperty("sprite_sequence")]
        string? SpriteSequence);

    internal record ColliderData(
        [property: JsonProperty("type")] string Type,
        [property: JsonProperty("radius")] double Radius);

    internal record FamilyData(
        [property: JsonProperty("description")]
        string Description,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("variants")] List<VariantData> Variants,
        [property: JsonProperty("blend")] string? Blend,
        [property: JsonProperty("collider")] ColliderData Collider);

    internal record BulletData(
        [property: JsonProperty("textures")] List<TextureData> Textures,
        [property: JsonProperty("sprites")] List<SpriteData> Sprites,
        [property: JsonProperty("sprite_sequences")]
        List<SpriteSequenceData> SpriteSequences,
        [property: JsonProperty("families")] Dictionary<string, FamilyData> Families);
}