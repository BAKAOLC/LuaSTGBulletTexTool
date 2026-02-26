using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TexCombineTool.Helpers;
using TexCombineTool.Models;

namespace TexCombineTool.Services
{
    using SpritePool = Dictionary<string, Image<Rgba32>>;

    internal class SpriteLoader
    {
        public static (
            Dictionary<string, SpritePool>,
            Dictionary<string, Dictionary<string, string[]>>,
            Dictionary<string, Dictionary<string, CropRange>>
            ) LoadTextureSprites(
                BulletData bulletData,
                SpritePool texturePool)
        {
            var poolTextureSprite = new Dictionary<string, SpritePool>();
            var poolSameSprite = new Dictionary<string, Dictionary<string, string[]>>();
            var poolTextureSpriteCropRange = new Dictionary<string, Dictionary<string, CropRange>>();
            var poolTextureSpriteRect = new Dictionary<string, Dictionary<Rectangle, List<string>>>();

            foreach (var (spriteName, textureName, rect, _, _, _) in bulletData.Sprites)
            {
                if (!texturePool.TryGetValue(textureName, out var texture))
                {
                    Console.WriteLine($"Texture not found for sprite: {textureName}");
                    continue;
                }

                if (!poolTextureSpriteRect.TryGetValue(textureName, out var spriteRectPool))
                {
                    spriteRectPool = [];
                    poolTextureSpriteRect[textureName] = spriteRectPool;
                }

                var spriteRect = rect.ToRectangle();
                var hasLoaded = spriteRectPool.TryGetValue(spriteRect, out var spriteNames);
                if (!hasLoaded)
                {
                    spriteNames = [];
                    spriteRectPool[spriteRect] = spriteNames;
                }

                spriteNames!.Add(spriteName);

                if (hasLoaded) continue;

                if (!poolTextureSprite.TryGetValue(textureName, out var spritePool))
                {
                    spritePool = [];
                    poolTextureSprite[textureName] = spritePool;
                }

                var spriteImage = texture.Clone(ctx => ctx.Crop(spriteRect));
                var cropRange = ImageHelper.CropEmptyPixels(spriteImage);

                if (!poolTextureSpriteCropRange.TryGetValue(textureName, out var poolCropRange))
                {
                    poolCropRange = [];
                    poolTextureSpriteCropRange[textureName] = poolCropRange;
                }

                poolCropRange[spriteName] = cropRange;
                Console.WriteLine(
                    $"Cropped sprite: {spriteName}, texture: {textureName}, rect: {spriteRect}, crop range: {cropRange}");
                spritePool.Add(spriteName, spriteImage);
            }

            foreach (var (textureName, rects) in poolTextureSpriteRect)
            {
                if (!poolSameSprite.TryGetValue(textureName, out var sameSprites))
                {
                    sameSprites = [];
                    poolSameSprite[textureName] = sameSprites;
                }

                foreach (var (_, spriteNames) in rects)
                {
                    if (spriteNames.Count <= 1) continue;
                    var mainSpriteName = spriteNames[0];
                    var sameSpriteNames = spriteNames.Skip(1).ToArray();
                    sameSprites[mainSpriteName] = sameSpriteNames;
                }
            }

            return (poolTextureSprite, poolSameSprite, poolTextureSpriteCropRange);
        }

        public static (SpritePool, Dictionary<string, string[]>) CombineSpritePools(
            Dictionary<string, SpritePool> poolTextureSprite,
            Dictionary<string, Dictionary<string, string[]>> poolSameSprite)
        {
            var poolCombinedSprite = new SpritePool();
            foreach (var (_, spritePool) in poolTextureSprite)
            foreach (var (spriteName, sprite) in spritePool)
                poolCombinedSprite.Add(spriteName, sprite);

            var poolCombinedSameSprite = new Dictionary<string, string[]>();
            foreach (var (_, sameSprites) in poolSameSprite)
            foreach (var (mainSpriteName, sameSpriteNames) in sameSprites)
                poolCombinedSameSprite[mainSpriteName] = sameSpriteNames;

            return (poolCombinedSprite, poolCombinedSameSprite);
        }

        public static Dictionary<string, SpriteData> MapSpriteData(
            List<SpriteData> spriteDataList,
            Dictionary<string, Dictionary<string, string[]>> sameSpritePool)
        {
            var spriteDataMap = new Dictionary<string, SpriteData>();
            foreach (var spriteData in spriteDataList) spriteDataMap[spriteData.Name] = spriteData;

            return spriteDataMap;
        }
    }
}