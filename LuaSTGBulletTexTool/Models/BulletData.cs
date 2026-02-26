using Newtonsoft.Json;

namespace TexCombineTool.Models
{
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