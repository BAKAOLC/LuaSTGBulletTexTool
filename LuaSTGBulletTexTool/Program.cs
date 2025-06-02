using System.Globalization;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TexCombineTool;
using Sprite = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SpritePool =
    System.Collections.Generic.Dictionary<string, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>>;

var startTime = DateTime.Now;

if (!ValidateAndParseArgs(args, out var jsonFilePath, out var width, out var height, out var margin, out var algorithm))
    return;

Console.WriteLine($"Loading bullet data from: {jsonFilePath}");
var bulletData = LoadBulletData(jsonFilePath);
if (bulletData == null)
{
    Console.WriteLine("Failed to load bullet data.");
    return;
}

Console.WriteLine($"Loaded bullet data from: {jsonFilePath}");

var texturePool = LoadTextures(bulletData, Path.GetDirectoryName(jsonFilePath) ?? string.Empty);
if (texturePool.Count == 0)
{
    Console.WriteLine("No textures loaded. Exiting.");
    return;
}

Console.WriteLine($"Loaded {texturePool.Count} textures.");

var (textureSpritePool, sameSpritePool, textureSpriteCropRangePool) = LoadTextureSprites(bulletData, texturePool);
if (textureSpritePool.Count == 0)
{
    Console.WriteLine("No texture sprites loaded. Exiting.");
    return;
}

var totalSpriteCount = textureSpritePool.Values.Sum(pool => pool.Count);
Console.WriteLine($"Loaded {totalSpriteCount} texture sprites.");

var outputDirectory = Path.Combine(Path.GetDirectoryName(jsonFilePath) ?? string.Empty, "generated");
if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

SaveSpritePools(textureSpritePool, outputDirectory);

var (combinedSpritePool, combinedSameSpritePool) = CombineSpritePools(textureSpritePool, sameSpritePool);
var spriteDataMap = MapSpriteData(bulletData.Sprites, sameSpritePool);
var cropRangePool = textureSpriteCropRangePool.SelectMany(x => x.Value)
    .ToDictionary(x => x.Key, x => x.Value);

var (combinedSprite, colorMapSprite, spriteDataList) =
    await CombineSprites("bullet_atlas", width, height,
            combinedSpritePool, spriteDataMap, combinedSameSpritePool, cropRangePool,
            margin, algorithm)
        .ConfigureAwait(false);

await SaveResults(outputDirectory, combinedSprite, colorMapSprite, bulletData, spriteDataList).ConfigureAwait(false);

Console.WriteLine("Completed");
Console.WriteLine($"Total time: {DateTime.Now - startTime}");

return;

bool ValidateAndParseArgs(string[] args, out string path, out int texWidth, out int texHeight, out int spriteMargin,
    out AlphaColorFixAlgorithm algorithmType)
{
    path = string.Empty;
    texWidth = 0;
    texHeight = 0;
    spriteMargin = 4;
    algorithmType = AlphaColorFixAlgorithm.Gaussian;

    if (args.Length < 3)
    {
        Console.WriteLine("Usage: <json> <width> <height> [margin] [algorithm]");
        Console.WriteLine("Available algorithms:");
        Console.WriteLine("  none     - Do not process transparent pixels");
        Console.WriteLine("  nearest  - Use nearest non-transparent pixel color");
        Console.WriteLine("  weighted - Use weighted average of surrounding pixels");
        Console.WriteLine("  gaussian - Use Gaussian weighted average (default)");
        return false;
    }

    path = args[0];
    if (!File.Exists(path))
    {
        Console.WriteLine($"File not found: {path}");
        return false;
    }

    if (!int.TryParse(args[1], out texWidth) || !int.TryParse(args[2], out texHeight))
    {
        Console.WriteLine("Invalid width or height.");
        return false;
    }

    if (texWidth <= 0 || texHeight <= 0)
    {
        Console.WriteLine("Width and height must be greater than 0.");
        return false;
    }

    if (args.Length > 3 && int.TryParse(args[3], out var marginValue))
    {
        if (marginValue < 0)
        {
            Console.WriteLine("Margin must be greater than or equal to 0.");
            return false;
        }

        spriteMargin = marginValue;
    }

    if (args.Length > 4)
        algorithmType = args[4].ToLower() switch
        {
            "none" => AlphaColorFixAlgorithm.None,
            "nearest" => AlphaColorFixAlgorithm.Nearest,
            "weighted" => AlphaColorFixAlgorithm.Weighted,
            _ => AlphaColorFixAlgorithm.Gaussian,
        };

    return true;
}

SpritePool LoadTextures(BulletData data, string baseDirectory)
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

(SpritePool, Dictionary<string, string[]>) CombineSpritePools(
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

async Task SaveResults(string outputDirectoryPath, Sprite combinedSpriteResult, Sprite colorMapSpriteResult,
    BulletData bulletDataResult,
    List<SpriteData> spriteDataListResult)
{
    var newTextureData = new TextureData("bullet_atlas", "bullet_atlas.png", false);
    var newBulletData = bulletDataResult with
    {
        Textures = [newTextureData],
        Sprites = spriteDataListResult.OrderBy(x => x.Name,
            StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)).ToList(),
    };

    Console.WriteLine("Saving texture...");
    var outputSpritePath = Path.Combine(outputDirectoryPath, "bullet_atlas.png");
    await combinedSpriteResult.SaveAsPngAsync(outputSpritePath).ConfigureAwait(false);
    Console.WriteLine($"Saved combined texture to: {outputSpritePath}");

    Console.WriteLine("Saving color map...");
    var outputColorMapPath = Path.Combine(outputDirectoryPath, "bullet_atlas_color_map.png");
    await colorMapSpriteResult.SaveAsPngAsync(outputColorMapPath).ConfigureAwait(false);
    Console.WriteLine($"Saved color map to: {outputColorMapPath}");

    Console.WriteLine("Saving bullet data...");
    var outputJsonPath = Path.Combine(outputDirectoryPath, "bullet_atlas.json");
    SaveBulletData(newBulletData, outputJsonPath);
    Console.WriteLine($"Saved bullet data to: {outputJsonPath}");
}

static async Task<(Sprite, Sprite, List<SpriteData>)> CombineSprites(string name, int width, int height,
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

    Console.WriteLine("Start to generate combined sprite");
    var x = 0;
    var y = 0;
    var mh = 0;
    var tasks = new List<Task>();
    foreach (var (spriteName, sprite) in spritePool.OrderByDescending(x => x.Value.Height)
                 .ThenBy(x => x.Key, StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)))
    {
        if (x + sprite.Width + margin * 2 > width)
        {
            x = 0;
            y += mh + margin * 2;
            mh = 0;
        }

        mh = Math.Max(mh, sprite.Height);
        if (y + mh + margin * 2 > height)
        {
            Console.WriteLine("Not enough space to combine sprites");
            break;
        }

        var point = new Point(x + margin, y + margin);
        var spriteRegion = new Rectangle(point.X - margin, point.Y - margin,
            sprite.Width + margin * 2, sprite.Height + margin * 2);
        spriteRegions.Add(spriteRegion);

        var originalSpriteData = spriteDataMap[spriteName];
        var rectData = new RectData(point.X, point.Y, sprite.Width, sprite.Height);
        var sameSpriteNames = sameSpritePool.GetValueOrDefault(spriteName, []);
        CropRange? cropRange = null;
        if (cropRangePool.TryGetValue(spriteName, out var range) ||
            sameSpriteNames.Any(sameSpriteName => cropRangePool.TryGetValue(sameSpriteName, out range)))
            cropRange = range;

        var centerData = FixCenterData(originalSpriteData, cropRange ?? new());
        resultSpriteData.Add(new(spriteName, name, rectData, centerData, originalSpriteData.Scaling,
            originalSpriteData.Blend));
        resultSpriteData.AddRange(sameSpriteNames.Select(sName => 
        {
            var sameSpriteData = spriteDataMap[sName];
            return new SpriteData(sName, name, rectData, centerData, 
                sameSpriteData.Scaling, sameSpriteData.Blend);
        }));

        tasks.Add(Task.Run(() => resultSprite.Mutate(ctx => ctx.DrawImage(sprite, point, 1f))));
        x += sprite.Width + margin * 2;
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
    tasks.Clear();

    Console.WriteLine("Generated combined texture");
    Console.WriteLine($"Start to fix alpha color using {algorithm} algorithm");
    var (finalResultSprite, colorMapSprite) = AlphaColorFixer.FixAlphaColor(resultSprite, spriteRegions, algorithm);
    Console.WriteLine("Completed");

    return (finalResultSprite, colorMapSprite, resultSpriteData);
}

static Dictionary<string, SpriteData> MapSpriteData(List<SpriteData> spriteDataList,
    Dictionary<string, Dictionary<string, string[]>> sameSpritePool)
{
    var spriteDataMap = new Dictionary<string, SpriteData>();
    foreach (var spriteData in spriteDataList)
    {
        var textureName = spriteData.Texture;
        var spriteName = spriteData.Name;
        spriteDataMap[spriteName] = spriteData;
    }

    return spriteDataMap;
}

static CropRange CropEmptyPixels(Sprite sprite)
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
        (minX == 0 && minY == 0 && maxX == sprite.Width - 1 && maxY == sprite.Height - 1)) return new(0, 0, 0, 0);
    var width = maxX - minX + 1;
    var height = maxY - minY + 1;
    sprite.Mutate(ctx => ctx.Crop(new(minX, minY, width, height)));

    var cropRange = new CropRange(minX, maxX, minY, maxY);
    return cropRange;
}

static CenterData FixCenterData(SpriteData spriteData, CropRange cropRange)
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

static void SaveSpritePool(SpritePool spritePool, string outputDirectory)
{
    foreach (var (name, image) in spritePool)
    {
        var outputPath = Path.Combine(outputDirectory, $"{name}.png");
        image.SaveAsPng(outputPath);
    }

    Console.WriteLine($"Saved {spritePool.Count} sprites to {outputDirectory}");
}

static BulletData? LoadBulletData(string jsonFilePath)
{
    var jsonContent = File.ReadAllText(jsonFilePath);
    var bulletData = JsonConvert.DeserializeObject<BulletData>(jsonContent);
    if (bulletData != null) return bulletData;
    Console.WriteLine("Failed to deserialize JSON content.");
    return null;
}

static void SaveBulletData(BulletData bulletData, string outputPath)
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

(Dictionary<string, SpritePool>, Dictionary<string, Dictionary<string, string[]>>,
    Dictionary<string, Dictionary<string, CropRange>>) LoadTextureSprites(BulletData definedBulletData,
        SpritePool loadedTexturePool)
{
    var poolTextureSprite = new Dictionary<string, SpritePool>();
    var poolSameSprite = new Dictionary<string, Dictionary<string, string[]>>();
    var poolTextureSpriteCropRange = new Dictionary<string, Dictionary<string, CropRange>>();
    var poolTextureSpriteRect = new Dictionary<string, Dictionary<Rectangle, List<string>>>();

    foreach (var (spriteName, s, rect, _, _, _) in definedBulletData.Sprites)
    {
        if (!loadedTexturePool.TryGetValue(s, out var texture))
        {
            Console.WriteLine($"Texture not found for sprite: {s}");
            continue;
        }

        if (!poolTextureSpriteRect.TryGetValue(s, out var spriteRectPool))
        {
            spriteRectPool = [];
            poolTextureSpriteRect[s] = spriteRectPool;
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

        if (!poolTextureSprite.TryGetValue(s, out var spritePool))
        {
            spritePool = [];
            poolTextureSprite[s] = spritePool;
        }

        var spriteImage = texture.Clone(ctx => ctx.Crop(spriteRect));
        var cropRange = CropEmptyPixels(spriteImage);
        if (!poolTextureSpriteCropRange.TryGetValue(s, out var poolCropRange))
        {
            poolCropRange = [];
            poolTextureSpriteCropRange[s] = poolCropRange;
        }

        poolCropRange[spriteName] = cropRange;
        Console.WriteLine($"Cropped sprite: {spriteName}, texture: {s}, rect: {spriteRect}, crop range: {cropRange}");
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

void SaveSpritePools(Dictionary<string, SpritePool> poolTextureSprite, string outputDirectoryPath)
{
    Console.WriteLine("Saving sprite pools...");
    foreach (var (textureName, spritePool) in poolTextureSprite)
    {
        var outputPath = Path.Combine(outputDirectoryPath, "sprites", textureName);
        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
        SaveSpritePool(spritePool, outputPath);
    }

    Console.WriteLine($"Saved {poolTextureSprite.Count} sprite pools to {outputDirectoryPath}");
}

// ReSharper disable NotAccessedPositionalProperty.Global
internal record struct CropRange(int Left, int Right, int Top, int Bottom);
// ReSharper restore NotAccessedPositionalProperty.Global