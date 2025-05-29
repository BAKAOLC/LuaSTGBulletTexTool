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

if (args.Length < 3)
{
    Console.WriteLine("Usage: <json> <width> <height> [margin] [algorithm]");
    Console.WriteLine("Available algorithms:");
    Console.WriteLine("  none     - Do not process transparent pixels");
    Console.WriteLine("  nearest  - Use nearest non-transparent pixel color");
    Console.WriteLine("  weighted - Use weighted average of surrounding pixels");
    Console.WriteLine("  gaussian - Use Gaussian weighted average (default)");
    return;
}

var jsonFilePath = args[0];
if (!File.Exists(jsonFilePath))
{
    Console.WriteLine($"File not found: {jsonFilePath}");
    return;
}

if (!int.TryParse(args[1], out var width) || !int.TryParse(args[2], out var height))
{
    Console.WriteLine("Invalid width or height.");
    return;
}

if (width <= 0 || height <= 0)
{
    Console.WriteLine("Width and height must be greater than 0.");
    return;
}

var margin = 4;
if (args.Length > 3 && int.TryParse(args[3], out var marginValue))
{
    if (marginValue < 0)
    {
        Console.WriteLine("Margin must be greater than or equal to 0.");
        return;
    }

    margin = marginValue;
}

var algorithm = AlphaColorFixAlgorithm.Gaussian;
if (args.Length > 4)
{
    algorithm = args[4].ToLower() switch
    {
        "none" => AlphaColorFixAlgorithm.None,
        "nearest" => AlphaColorFixAlgorithm.Nearest,
        "weighted" => AlphaColorFixAlgorithm.Weighted,
        "gaussian" => AlphaColorFixAlgorithm.Gaussian,
        _ => AlphaColorFixAlgorithm.Gaussian
    };
}

Console.WriteLine($"Loading bullet data from: {jsonFilePath}");
var bulletData = LoadBulletData(jsonFilePath);
if (bulletData == null)
{
    Console.WriteLine("Failed to load bullet data.");
    return;
}

Console.WriteLine($"Loaded bullet data from: {jsonFilePath}");

Console.WriteLine("Loading textures...");
var texturePool = new SpritePool();
LoadTextures(bulletData, Path.GetDirectoryName(jsonFilePath) ?? string.Empty, texturePool);
if (texturePool.Count == 0)
{
    Console.WriteLine("No textures loaded. Exiting.");
    return;
}

Console.WriteLine($"Loaded {texturePool.Count} textures.");

Console.WriteLine("Loading texture sprites...");
var textureSpritePool = new Dictionary<string, SpritePool>();
var sameSpritePool = new Dictionary<string, Dictionary<string, string[]>>();
var textureSpriteCropRangePool = new Dictionary<string, Dictionary<string, CropRange>>();
LoadTextureSprite(bulletData, texturePool, textureSpritePool, sameSpritePool, textureSpriteCropRangePool);
if (textureSpritePool.Count == 0)
{
    Console.WriteLine("No texture sprites loaded. Exiting.");
    return;
}

var totalSpriteCount = textureSpritePool.Values.Sum(pool => pool.Count);
Console.WriteLine($"Loaded {totalSpriteCount} texture sprites.");

var outputDirectory = Path.Combine(Path.GetDirectoryName(jsonFilePath) ?? string.Empty, "generated");
if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

Console.WriteLine("Saving sprite pools...");
foreach (var (textureName, spritePool) in textureSpritePool)
{
    var outputPath = Path.Combine(outputDirectory, "sprites", textureName);
    if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
    SaveSpritePool(spritePool, outputPath);
}

Console.WriteLine($"Saved {textureSpritePool.Count} sprite pools to {outputDirectory}");

Console.WriteLine("Combining sprites...");
var combinedSpritePool = new SpritePool();
foreach (var (_, spritePool) in textureSpritePool)
    foreach (var (spriteName, sprite) in spritePool)
        combinedSpritePool.Add(spriteName, sprite);
var combinedSameSpritePool = new Dictionary<string, string[]>();
foreach (var (_, sameSprites) in sameSpritePool)
    foreach (var (mainSpriteName, sameSpriteNames) in sameSprites)
        combinedSameSpritePool[mainSpriteName] = sameSpriteNames;
var spriteDataMap = MapSpriteData(bulletData.Sprites, sameSpritePool);
var cropRangePool = textureSpriteCropRangePool.SelectMany(x => x.Value)
    .ToDictionary(x => x.Key, x => x.Value);
var (combinedSprite, colorMapSprite, spriteDataList) =
    await CombineSprites("bullet_atlas", width, height,
            combinedSpritePool, spriteDataMap, combinedSameSpritePool, cropRangePool,
            margin, algorithm)
        .ConfigureAwait(false);

var newTextureData = new TextureData("bullet_atlas", "bullet_atlas.png", false);
var newBulletData = bulletData with
{
    Textures = [newTextureData],
    Sprites = spriteDataList.OrderBy(x => x.Name,
        StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)).ToList(),
};
Console.WriteLine("Saving texture...");
var outputSpritePath = Path.Combine(outputDirectory, "bullet_atlas.png");
await combinedSprite.SaveAsPngAsync(outputSpritePath).ConfigureAwait(false);
Console.WriteLine($"Saved combined texture to: {outputSpritePath}");
Console.WriteLine("Saving color map...");
var outputColorMapPath = Path.Combine(outputDirectory, "bullet_atlas_color_map.png");
await colorMapSprite.SaveAsPngAsync(outputColorMapPath).ConfigureAwait(false);
Console.WriteLine($"Saved color map to: {outputColorMapPath}");
Console.WriteLine("Saving bullet data...");
var outputJsonPath = Path.Combine(outputDirectory, "bullet_atlas.json");
SaveBulletData(newBulletData, outputJsonPath);
Console.WriteLine($"Saved bullet data to: {outputJsonPath}");
Console.WriteLine("Completed");
Console.WriteLine($"Total time: {DateTime.Now - startTime}");

return;

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
        resultSpriteData.AddRange(sameSpriteNames.Select(sName => new SpriteData(sName, name,
            rectData, centerData, originalSpriteData.Scaling, originalSpriteData.Blend)));

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
        if (!sameSpritePool.TryGetValue(textureName, out var sameSprites))
        {
            spriteDataMap[spriteName] = spriteData;
            continue;
        }

        var mainSpriteName = sameSprites.Values.FirstOrDefault(x => x.Contains(spriteName))?.FirstOrDefault() ??
                             spriteName;
        spriteDataMap[mainSpriteName] = spriteData;
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

    var offsetX = cropRange.left;
    var offsetY = cropRange.top;
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

static void LoadTextures(BulletData bulletData, string baseDirectory, SpritePool texturePool)
{
    foreach (var texture in bulletData.Textures)
    {
        var texturePath = Path.Combine(baseDirectory, texture.Path);
        if (!File.Exists(texturePath))
        {
            Console.WriteLine($"Texture file not found: {texturePath}");
            continue;
        }

        var image = Image.Load<Rgba32>(texturePath);
        texturePool[texture.Name] = image;
    }
}

static void LoadTextureSprite(BulletData bulletData, SpritePool texturePool,
    Dictionary<string, SpritePool> textureSpritePool,
    Dictionary<string, Dictionary<string, string[]>> sameSpritePool,
    Dictionary<string, Dictionary<string, CropRange>> textureSpriteCropRangePool)
{
    var textureSpriteRectPool = new Dictionary<string, Dictionary<Rectangle, List<string>>>();
    foreach (var (spriteName, s, rect, _, _, _) in bulletData.Sprites)
    {
        if (!texturePool.TryGetValue(s, out var texture))
        {
            Console.WriteLine($"Texture not found for sprite: {s}");
            continue;
        }

        if (!textureSpriteRectPool.TryGetValue(s, out var spriteRectPool))
        {
            spriteRectPool = [];
            textureSpriteRectPool[s] = spriteRectPool;
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

        if (!textureSpritePool.TryGetValue(s, out var spritePool))
        {
            spritePool = [];
            textureSpritePool[s] = spritePool;
        }

        var spriteImage = texture.Clone(ctx => ctx.Crop(spriteRect));
        var cropRange = CropEmptyPixels(spriteImage);
        if (!textureSpriteCropRangePool.TryGetValue(s, out var cropRangePool))
        {
            cropRangePool = [];
            textureSpriteCropRangePool[s] = cropRangePool;
        }

        cropRangePool[spriteName] = cropRange;
        Console.WriteLine($"Cropped sprite: {spriteName}, texture: {s}, rect: {spriteRect}, crop range: {cropRange}");
        spritePool.Add(spriteName, spriteImage);
    }

    foreach (var (textureName, rects) in textureSpriteRectPool)
    {
        if (!sameSpritePool.TryGetValue(textureName, out var sameSprites))
        {
            sameSprites = [];
            sameSpritePool[textureName] = sameSprites;
        }

        foreach (var (_, spriteNames) in rects)
        {
            if (spriteNames.Count <= 1) continue;
            var mainSpriteName = spriteNames[0];
            var sameSpriteNames = spriteNames.Skip(1).ToArray();
            sameSprites[mainSpriteName] = sameSpriteNames;
        }
    }
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

internal static class ImageExtension
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

    private static float CalculateWeight(float distance)
    {
        const float sigma = 2.0f;
        return (float)Math.Exp(-(distance * distance) / (2 * sigma * sigma));
    }

    internal static Rgba32 CalculateWeightedColor(Sprite sprite, int x, int y, bool[,] processed, List<Rectangle> spriteRegions)
    {
        var currentRegion = spriteRegions.FirstOrDefault(r =>
            x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);

        if (currentRegion == default) return new Rgba32();

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
            var weight = CalculateWeight(distance);
            totalWeight += weight;

            weightedR += pixel.R * weight;
            weightedG += pixel.G * weight;
            weightedB += pixel.B * weight;
        }

        if (totalWeight <= 0) return new Rgba32();

        return new Rgba32(
            (byte)(weightedR / totalWeight),
            (byte)(weightedG / totalWeight),
            (byte)(weightedB / totalWeight),
            0
        );
    }

    internal static int GetNotTransparentNeighborsPixelColor(Sprite sprite, int x, int y, bool[,] processed,
        out Rgba32[] result, List<Rectangle> spriteRegions)
    {
        result = new Rgba32[8];
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
            result[count] = pixel;
            count++;
        }

        return count;
    }
}

internal record struct CropRange(int left, int right, int top, int bottom);