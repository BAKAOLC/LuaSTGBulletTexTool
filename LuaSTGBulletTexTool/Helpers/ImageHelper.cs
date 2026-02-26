using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TexCombineTool.Models;

namespace TexCombineTool.Helpers
{
    using Sprite = Image<Rgba32>;

    internal static class ImageHelper
    {
        public static CropRange CropEmptyPixels(Sprite sprite)
        {
            var minX = sprite.Width;
            var minY = sprite.Height;
            var maxX = 0;
            var maxY = 0;

            for (var y = 0; y < sprite.Height; y++)
            for (var x = 0; x < sprite.Width; x++)
            {
                var pixel = sprite[x, y];
                if (pixel.A <= 0) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            if (minX >= maxX || minY >= maxY ||
                (minX == 0 && minY == 0 && maxX == sprite.Width - 1 && maxY == sprite.Height - 1))
                return new(0, 0, 0, 0);

            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            sprite.Mutate(ctx => ctx.Crop(new(minX, minY, width, height)));

            return new(minX, maxX, minY, maxY);
        }

        public static CenterData FixCenterData(SpriteData spriteData, CropRange cropRange)
        {
            double centerX, centerY;
            if (spriteData.Center != null)
            {
                centerX = spriteData.Center.X;
                centerY = spriteData.Center.Y;
            }
            else
            {
                centerX = spriteData.Rect.Width / 2d;
                centerY = spriteData.Rect.Height / 2d;
            }

            var offsetX = cropRange.Left;
            var offsetY = cropRange.Top;
            centerX -= offsetX;
            centerY -= offsetY;
            return new(centerX, centerY);
        }
    }
}