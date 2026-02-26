using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TexCombineTool
{
    using Sprite = Image<Rgba32>;

    internal enum AlphaColorFixAlgorithm
    {
        None, // 不处理透明像素
        Nearest, // 使用最近的非透明像素颜色
        Weighted, // 使用加权平均
        Gaussian, // 使用高斯加权
    }

    internal static class AlphaColorFixer
    {
        private static readonly (int, int)[] OffsetList =
        [
            (1, 0), (0, 1), (-1, 0), (0, -1),
            (1, 1), (-1, 1), (-1, -1), (1, -1),
        ];

        private static readonly (int, int)[] ExtendedOffsetList =
        [
            (1, 0), (0, 1), (-1, 0), (0, -1),
            (1, 1), (-1, 1), (-1, -1), (1, -1),
            (2, 0), (0, 2), (-2, 0), (0, -2),
            (2, 1), (1, 2), (-1, 2), (-2, 1),
            (-2, -1), (-1, -2), (1, -2), (2, -1),
        ];

        private static float CalculateGaussianWeight(float distance)
        {
            const float sigma = 2.0f;
            return (float)Math.Exp(-(distance * distance) / (2 * sigma * sigma));
        }

        private static float CalculateLinearWeight(float distance)
        {
            const float maxDistance = 3.0f;
            return Math.Max(0, 1 - distance / maxDistance);
        }

        private static Rgba32 CalculateNearestColor(Sprite sprite, int x, int y, bool[,] processed,
            List<Rectangle> spriteRegions)
        {
            var currentRegion = spriteRegions.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

            if (currentRegion == default) return new();

            foreach (var (ox, oy) in OffsetList)
            {
                var nx = x + ox;
                var ny = y + oy;

                if (nx < currentRegion.X || nx >= currentRegion.X + currentRegion.Width ||
                    ny < currentRegion.Y || ny >= currentRegion.Y + currentRegion.Height)
                    continue;

                var pixel = sprite[nx, ny];
                if (!processed[nx, ny] && pixel.A == 0) continue;

                return new(pixel.R, pixel.G, pixel.B, 0);
            }

            return new();
        }

        private static Rgba32 CalculateWeightedColor(Sprite sprite, int x, int y, bool[,] processed,
            List<Rectangle> spriteRegions)
        {
            var currentRegion = spriteRegions.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

            if (currentRegion == default) return new();

            var totalWeight = 0f;
            var weightedR = 0f;
            var weightedG = 0f;
            var weightedB = 0f;

            foreach (var (ox, oy) in OffsetList)
            {
                var nx = x + ox;
                var ny = y + oy;

                if (nx < currentRegion.X || nx >= currentRegion.X + currentRegion.Width ||
                    ny < currentRegion.Y || ny >= currentRegion.Y + currentRegion.Height)
                    continue;

                var pixel = sprite[nx, ny];
                if (!processed[nx, ny] && pixel.A == 0) continue;

                var distance = (float)Math.Sqrt(ox * ox + oy * oy);
                var weight = CalculateLinearWeight(distance);
                totalWeight += weight;

                weightedR += pixel.R * weight;
                weightedG += pixel.G * weight;
                weightedB += pixel.B * weight;
            }

            if (totalWeight <= 0) return new();

            return new(
                (byte)(weightedR / totalWeight),
                (byte)(weightedG / totalWeight),
                (byte)(weightedB / totalWeight),
                0
            );
        }

        private static Rgba32 CalculateGaussianColor(Sprite sprite, int x, int y, bool[,] processed,
            List<Rectangle> spriteRegions)
        {
            var currentRegion = spriteRegions.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

            if (currentRegion == default) return new();

            var totalWeight = 0f;
            var weightedR = 0f;
            var weightedG = 0f;
            var weightedB = 0f;

            foreach (var (ox, oy) in ExtendedOffsetList)
            {
                var nx = x + ox;
                var ny = y + oy;

                if (nx < currentRegion.X || nx >= currentRegion.X + currentRegion.Width ||
                    ny < currentRegion.Y || ny >= currentRegion.Y + currentRegion.Height)
                    continue;

                var pixel = sprite[nx, ny];
                if (!processed[nx, ny] && pixel.A == 0) continue;

                var distance = (float)Math.Sqrt(ox * ox + oy * oy);
                var weight = CalculateGaussianWeight(distance);
                totalWeight += weight;

                weightedR += pixel.R * weight;
                weightedG += pixel.G * weight;
                weightedB += pixel.B * weight;
            }

            if (totalWeight <= 0) return new();

            return new(
                (byte)(weightedR / totalWeight),
                (byte)(weightedG / totalWeight),
                (byte)(weightedB / totalWeight),
                0
            );
        }

        public static (Sprite, Sprite) FixAlphaColor(Sprite sprite, List<Rectangle> spriteRegions,
            AlphaColorFixAlgorithm algorithm)
        {
            var width = sprite.Width;
            var height = sprite.Height;
            if (width == 0 || height == 0) return (sprite, sprite);

            var colorMap = new Sprite(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var pixel = sprite[x, y];
                colorMap[x, y] = new(pixel.R, pixel.G, pixel.B, 255);
            }

            if (algorithm == AlphaColorFixAlgorithm.None) return (sprite, colorMap);

            var processed = new bool[width, height];
            var regionLocks = new Dictionary<Rectangle, object>();
            foreach (var region in spriteRegions)
                regionLocks[region] = new();

            Parallel.ForEach(spriteRegions, region =>
            {
                for (var y = region.Y; y < region.Y + region.Height; y++)
                for (var x = region.X; x < region.X + region.Width; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;
                    var pixel = sprite[x, y];
                    if (pixel.A <= 0) continue;
                    lock (regionLocks[region])
                    {
                        processed[x, y] = true;
                    }
                }
            });

            Parallel.ForEach(spriteRegions, region =>
            {
                var changed = true;
                while (changed)
                {
                    changed = false;
                    var pixelsToProcess = new List<(int x, int y)>();

                    for (var y = region.Y; y < region.Y + region.Height; y++)
                    for (var x = region.X; x < region.X + region.Width; x++)
                    {
                        if (x < 0 || x >= width || y < 0 || y >= height) continue;
                        if (processed[x, y]) continue;

                        var pixel = sprite[x, y];
                        if (pixel.A > 0) continue;

                        var count = GetNotTransparentNeighborsPixelColor(sprite, x, y, processed, spriteRegions);
                        if (count <= 0) continue;

                        pixelsToProcess.Add((x, y));
                    }

                    if (pixelsToProcess.Count <= 0) continue;
                    {
                        lock (regionLocks[region])
                        {
                            foreach (var (x, y) in pixelsToProcess)
                            {
                                if (processed[x, y]) continue;

                                var color = algorithm switch
                                {
                                    AlphaColorFixAlgorithm.Nearest => CalculateNearestColor(sprite, x, y, processed,
                                        spriteRegions),
                                    AlphaColorFixAlgorithm.Weighted => CalculateWeightedColor(sprite, x, y, processed,
                                        spriteRegions),
                                    AlphaColorFixAlgorithm.Gaussian => CalculateGaussianColor(sprite, x, y, processed,
                                        spriteRegions),
                                    _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null),
                                };

                                sprite[x, y] = color;
                                colorMap[x, y] = new(color.R, color.G, color.B, 255);
                                processed[x, y] = true;
                                changed = true;
                            }
                        }
                    }
                }
            });

            return (sprite, colorMap);
        }

        private static int GetNotTransparentNeighborsPixelColor(Sprite sprite, int x, int y, bool[,] processed,
            List<Rectangle> spriteRegions)
        {
            var count = 0;
            if (x < 0 || x >= sprite.Width || y < 0 || y >= sprite.Height) return 0;

            var currentRegion = spriteRegions.FirstOrDefault(r =>
                x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

            if (currentRegion == default) return 0;

            foreach (var (ox, oy) in OffsetList)
            {
                var nx = x + ox;
                var ny = y + oy;

                if (nx < currentRegion.X || nx >= currentRegion.X + currentRegion.Width ||
                    ny < currentRegion.Y || ny >= currentRegion.Y + currentRegion.Height)
                    continue;

                var pixel = sprite[nx, ny];
                if (!processed[nx, ny] && pixel.A == 0) continue;
                count++;
            }

            return count;
        }
    }
}