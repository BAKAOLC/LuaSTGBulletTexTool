using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TexCombineTool.Helpers;
using TexCombineTool.Models;

namespace TexCombineTool.Services
{
    using Sprite = Image<Rgba32>;
    using SpritePool = Dictionary<string, Image<Rgba32>>;

    internal class OutputService
    {
        public static async Task SaveResults(
            string outputDirectoryPath,
            Sprite combinedSprite,
            Sprite colorMapSprite,
            BulletData bulletData,
            List<SpriteData> spriteDataList)
        {
            var newTextureData = new TextureData("bullet_atlas", "bullet_atlas.png", false);
            var newBulletData = bulletData with
            {
                Textures = [newTextureData],
                Sprites = spriteDataList.OrderBy(x => x.Name,
                    StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)).ToList(),
            };

            Console.WriteLine("Saving texture...");
            var outputSpritePath = Path.Combine(outputDirectoryPath, "bullet_atlas.png");
            await combinedSprite.SaveAsPngAsync(outputSpritePath).ConfigureAwait(false);
            Console.WriteLine($"Saved combined texture to: {outputSpritePath}");

            Console.WriteLine("Saving color map...");
            var outputColorMapPath = Path.Combine(outputDirectoryPath, "bullet_atlas_color_map.png");
            await colorMapSprite.SaveAsPngAsync(outputColorMapPath).ConfigureAwait(false);
            Console.WriteLine($"Saved color map to: {outputColorMapPath}");

            Console.WriteLine("Saving bullet data...");
            var outputJsonPath = Path.Combine(outputDirectoryPath, "bullet_atlas.json");
            FileHelper.SaveBulletData(newBulletData, outputJsonPath);
            Console.WriteLine($"Saved bullet data to: {outputJsonPath}");
        }

        public static void SaveSpritePools(Dictionary<string, Dictionary<string, Sprite>> poolTextureSprite,
            string outputDirectoryPath)
        {
            Console.WriteLine("Saving sprite pools...");
            foreach (var (textureName, spritePool) in poolTextureSprite)
            {
                var outputPath = Path.Combine(outputDirectoryPath, "sprites", textureName);
                if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
                FileHelper.SaveSpritePool(spritePool, outputPath);
            }

            Console.WriteLine($"Saved {poolTextureSprite.Count} sprite pools to {outputDirectoryPath}");
        }

        public static async Task SaveSplitJson(
            string outputDirectoryPath,
            BulletData bulletData,
            SpritePool spritePool,
            Dictionary<string, SpriteData> spriteDataMap,
            Dictionary<string, string[]> sameSpritePool,
            Dictionary<string, CropRange> cropRangePool)
        {
            Console.WriteLine("Generating split sprites JSON...");

            var newTextures = new List<TextureData>();
            var newSprites = new List<SpriteData>();

            var spriteToOriginalTexture = new Dictionary<string, string>();
            foreach (var spriteData in bulletData.Sprites)
                spriteToOriginalTexture[spriteData.Name] = spriteData.Texture;

            foreach (var (spriteName, sprite) in spritePool)
            {
                var originalTextureName = spriteToOriginalTexture.GetValueOrDefault(spriteName, "unknown");

                var textureName = $"sprite_{spriteName}";
                var texturePath = $"sprites/{originalTextureName}/{spriteName}.png";
                newTextures.Add(new(textureName, texturePath, false));

                var originalSpriteData = spriteDataMap[spriteName];

                var rectData = new RectData(0, 0, sprite.Width, sprite.Height);

                CropRange? cropRange = null;
                if (cropRangePool.TryGetValue(spriteName, out var range))
                    cropRange = range;

                var centerData = ImageHelper.FixCenterData(originalSpriteData, cropRange ?? new());

                newSprites.Add(new(
                    spriteName,
                    textureName,
                    rectData,
                    centerData,
                    originalSpriteData.Scaling,
                    originalSpriteData.Blend));

                if (sameSpritePool.TryGetValue(spriteName, out var sameSpriteNames))
                    newSprites.AddRange(from sName in sameSpriteNames
                        let sameSpriteData = spriteDataMap[sName]
                        select new SpriteData(sName, textureName, rectData, centerData, sameSpriteData.Scaling,
                            sameSpriteData.Blend));
            }

            var newBulletData = bulletData with
            {
                Textures = newTextures.OrderBy(x => x.Name,
                    StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)).ToList(),
                Sprites = newSprites.OrderBy(x => x.Name,
                    StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)).ToList(),
            };

            Console.WriteLine("Saving split bullet data...");
            var outputJsonPath = Path.Combine(outputDirectoryPath, "bullet_split.json");
            FileHelper.SaveBulletData(newBulletData, outputJsonPath);
            Console.WriteLine($"Saved split bullet data to: {outputJsonPath}");

            await Task.CompletedTask;
        }
    }
}