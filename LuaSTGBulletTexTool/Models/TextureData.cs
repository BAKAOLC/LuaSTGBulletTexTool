using System.Text.Json.Serialization;

namespace TexCombineTool.Models
{
    internal record TextureData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("mipmap")] bool Mipmap);
}