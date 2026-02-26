using Newtonsoft.Json;

namespace TexCombineTool.Models
{
    internal record TextureData(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("path")] string Path,
        [property: JsonProperty("mipmap")] bool Mipmap);
}