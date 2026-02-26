using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TexCombineTool.Helpers;
using TexCombineTool.Models;

namespace TexCombineTool.Services
{
    using Sprite = Image<Rgba32>;

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
    }
}