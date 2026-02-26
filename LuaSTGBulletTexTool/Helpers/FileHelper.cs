using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TexCombineTool.Models;

namespace TexCombineTool.Helpers
{
    using SpritePool = Dictionary<string, Image<Rgba32>>;

    internal static class FileHelper
    {
        public static BulletData? LoadBulletData(string jsonFilePath)
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var bulletData = JsonConvert.DeserializeObject<BulletData>(jsonContent);
            if (bulletData != null) return bulletData;
            Console.WriteLine("Failed to deserialize JSON content.");
            return null;
        }

        public static void SaveBulletData(BulletData bulletData, string outputPath)
        {
            var jsonSerializeOptions = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
            };
            var resultJson = JsonConvert.SerializeObject(bulletData, jsonSerializeOptions);
            File.WriteAllText(outputPath, resultJson);
            Console.WriteLine($"Generated JSON file: {outputPath}");
        }

        public static SpritePool LoadTextures(BulletData data, string baseDirectory)
        {
            var pool = new SpritePool();
            foreach (var texture in data.Textures)
            {
                var texturePath = Path.Combine(baseDirectory, texture.Path);
                if (!File.Exists(texturePath))
                {
                    Console.WriteLine($"Texture file not found: {texturePath}");
                    continue;
                }

                var image = Image.Load<Rgba32>(texturePath);
                pool[texture.Name] = image;
            }

            return pool;
        }

        public static void SaveSpritePool(SpritePool spritePool, string outputDirectory)
        {
            foreach (var (name, image) in spritePool)
            {
                var outputPath = Path.Combine(outputDirectory, $"{name}.png");
                image.SaveAsPng(outputPath);
            }

            Console.WriteLine($"Saved {spritePool.Count} sprites to {outputDirectory}");
        }
    }
}