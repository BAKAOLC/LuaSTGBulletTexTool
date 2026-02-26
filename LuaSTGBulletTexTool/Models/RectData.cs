using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace TexCombineTool.Models
{
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
}