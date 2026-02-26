using System.Text.Json.Serialization;

namespace TexCombineTool.Models
{
    internal record VariantData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("sprite")] string? Sprite,
        [property: JsonPropertyName("sprite_sequence")]
        string? SpriteSequence);

    internal record ColliderData(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("radius")] double Radius);

    internal record FamilyData(
        [property: JsonPropertyName("description")]
        string Description,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("variants")]
        List<VariantData> Variants,
        [property: JsonPropertyName("blend")] string? Blend,
        [property: JsonPropertyName("collider")]
        ColliderData Collider);

    internal record BulletData(
        [property: JsonPropertyName("textures")]
        List<TextureData> Textures,
        [property: JsonPropertyName("sprites")]
        List<SpriteData> Sprites,
        [property: JsonPropertyName("sprite_sequences")]
        List<SpriteSequenceData> SpriteSequences,
        [property: JsonPropertyName("families")]
        Dictionary<string, FamilyData> Families);
}