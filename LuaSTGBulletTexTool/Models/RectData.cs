using System.Text.Json.Serialization;
using SixLabors.ImageSharp;

namespace TexCombineTool.Models
{
    internal record RectData(
        [property: JsonPropertyName("x")] int X,
        [property: JsonPropertyName("y")] int Y,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height);

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