using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TexCombineTool.Helpers;
using TexCombineTool.Layout;
using TexCombineTool.Models;

namespace TexCombineTool.Services
{
    using Sprite = Image<Rgba32>;
    using SpritePool = Dictionary<string, Image<Rgba32>>;

    internal class SpriteCombiner(ILayoutAlgorithm layoutAlgorithm)
    {
        public async Task<(Sprite, Sprite, List<SpriteData>)> CombineSprites(
            string name,
            int width,
            int height,
            SpritePool spritePool,
            Dictionary<string, SpriteData> spriteDataMap,
            Dictionary<string, string[]> sameSpritePool,
            Dictionary<string, CropRange> cropRangePool,
            int margin = 4,
            AlphaColorFixAlgorithm algorithm = AlphaColorFixAlgorithm.Gaussian)
        {
            Console.WriteLine($"Combining {spritePool.Count} sprites into {name}");
            Console.WriteLine($"Create texture with size: {width}x{height}");

            var resultSprite = new Sprite(width, height);
            var resultSpriteData = new List<SpriteData>();
            var spriteRegions = new List<Rectangle>();

            var spriteSizes = spritePool.ToDictionary(
                kvp => kvp.Key,
                kvp => new Size(kvp.Value.Width, kvp.Value.Height));

            Console.WriteLine("Start to generate combined sprite using layout algorithm");
            var layout = layoutAlgorithm.Layout(spriteSizes, width, height, margin);

            var tasks = new List<Task>();
            foreach (var (spriteName, rect) in layout)
            {
                if (!spritePool.TryGetValue(spriteName, out var sprite))
                    continue;

                var spriteRegion = new Rectangle(
                    rect.X - margin,
                    rect.Y - margin,
                    sprite.Width + margin * 2,
                    sprite.Height + margin * 2);
                spriteRegions.Add(spriteRegion);

                var originalSpriteData = spriteDataMap[spriteName];
                var rectData = new RectData(rect.X, rect.Y, sprite.Width, sprite.Height);
                var sameSpriteNames = sameSpritePool.GetValueOrDefault(spriteName, []);

                CropRange? cropRange = null;
                if (cropRangePool.TryGetValue(spriteName, out var range) ||
                    sameSpriteNames.Any(sameSpriteName => cropRangePool.TryGetValue(sameSpriteName, out range)))
                    cropRange = range;

                var centerData = ImageHelper.FixCenterData(originalSpriteData, cropRange ?? new());
                resultSpriteData.Add(new(spriteName, name, rectData, centerData,
                    originalSpriteData.Scaling, originalSpriteData.Blend));

                resultSpriteData.AddRange(sameSpriteNames.Select(sName =>
                {
                    var sameSpriteData = spriteDataMap[sName];
                    return new SpriteData(sName, name, rectData, centerData,
                        sameSpriteData.Scaling, sameSpriteData.Blend);
                }));

                tasks.Add(Task.Run(() =>
                    resultSprite.Mutate(ctx => ctx.DrawImage(sprite, new Point(rect.X, rect.Y), 1f))));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            tasks.Clear();

            Console.WriteLine("Generated combined texture");
            Console.WriteLine($"Start to fix alpha color using {algorithm} algorithm");
            var (finalResultSprite, colorMapSprite) =
                AlphaColorFixer.FixAlphaColor(resultSprite, spriteRegions, algorithm);
            Console.WriteLine("Completed");

            return (finalResultSprite, colorMapSprite, resultSpriteData);
        }
    }
}