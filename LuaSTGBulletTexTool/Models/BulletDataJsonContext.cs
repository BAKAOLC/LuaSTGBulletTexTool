using System.Text.Json.Serialization;

namespace TexCombineTool.Models
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
    [JsonSerializable(typeof(BulletData))]
    [JsonSerializable(typeof(TextureData))]
    [JsonSerializable(typeof(SpriteData))]
    [JsonSerializable(typeof(SpriteSequenceData))]
    [JsonSerializable(typeof(FamilyData))]
    [JsonSerializable(typeof(VariantData))]
    [JsonSerializable(typeof(ColliderData))]
    [JsonSerializable(typeof(CenterData))]
    [JsonSerializable(typeof(RectData))]
    internal partial class BulletDataJsonContext : JsonSerializerContext
    {
    }
}