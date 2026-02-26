using TexCombineTool;
using TexCombineTool.Helpers;
using TexCombineTool.Layout;
using TexCombineTool.Services;

var startTime = DateTime.Now;

if (!ValidateAndParseArgs(args, out var jsonFilePath, out var width, out var height, out var margin,
        out var algorithm, out var useMaxRects))
    return;

Console.WriteLine($"Loading bullet data from: {jsonFilePath}");
var bulletData = FileHelper.LoadBulletData(jsonFilePath);
if (bulletData == null)
{
    Console.WriteLine("Failed to load bullet data.");
    return;
}

Console.WriteLine($"Loaded bullet data from: {jsonFilePath}");

var texturePool = FileHelper.LoadTextures(bulletData, Path.GetDirectoryName(jsonFilePath) ?? string.Empty);
if (texturePool.Count == 0)
{
    Console.WriteLine("No textures loaded. Exiting.");
    return;
}

Console.WriteLine($"Loaded {texturePool.Count} textures.");

var (textureSpritePool, sameSpritePool, textureSpriteCropRangePool) =
    SpriteLoader.LoadTextureSprites(bulletData, texturePool);

if (textureSpritePool.Count == 0)
{
    Console.WriteLine("No texture sprites loaded. Exiting.");
    return;
}

var totalSpriteCount = textureSpritePool.Values.Sum(pool => pool.Count);
Console.WriteLine($"Loaded {totalSpriteCount} texture sprites.");

var outputDirectory = Path.Combine(Path.GetDirectoryName(jsonFilePath) ?? string.Empty, "generated");
if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

OutputService.SaveSpritePools(textureSpritePool, outputDirectory);

var (combinedSpritePool, combinedSameSpritePool) = SpriteLoader.CombineSpritePools(textureSpritePool, sameSpritePool);
var spriteDataMap = SpriteLoader.MapSpriteData(bulletData.Sprites, sameSpritePool);
var cropRangePool = textureSpriteCropRangePool.SelectMany(x => x.Value)
    .ToDictionary(x => x.Key, x => x.Value);

ILayoutAlgorithm layoutAlgorithm = useMaxRects
    ? new MaxRectsLayoutAlgorithm()
    : new SimpleRowLayoutAlgorithm();

Console.WriteLine($"Using layout algorithm: {layoutAlgorithm.GetType().Name}");

var spriteCombiner = new SpriteCombiner(layoutAlgorithm);
var (combinedSprite, colorMapSprite, spriteDataList) =
    await spriteCombiner.CombineSprites("bullet_atlas", width, height,
            combinedSpritePool, spriteDataMap, combinedSameSpritePool, cropRangePool,
            margin, algorithm)
        .ConfigureAwait(false);

await OutputService.SaveResults(outputDirectory, combinedSprite, colorMapSprite, bulletData, spriteDataList)
    .ConfigureAwait(false);

Console.WriteLine("Completed");
Console.WriteLine($"Total time: {DateTime.Now - startTime}");

return;

bool ValidateAndParseArgs(string[] args, out string path, out int texWidth, out int texHeight, out int spriteMargin,
    out AlphaColorFixAlgorithm algorithmType, out bool useMaxRectsLayout)
{
    path = string.Empty;
    texWidth = 0;
    texHeight = 0;
    spriteMargin = 4;
    algorithmType = AlphaColorFixAlgorithm.Gaussian;
    useMaxRectsLayout = true;

    if (args.Length < 3)
    {
        Console.WriteLine("Usage: <json> <width> <height> [margin] [algorithm] [layout]");
        Console.WriteLine();
        Console.WriteLine("Available algorithms:");
        Console.WriteLine("  none     - Do not process transparent pixels");
        Console.WriteLine("  nearest  - Use nearest non-transparent pixel color");
        Console.WriteLine("  weighted - Use weighted average of surrounding pixels");
        Console.WriteLine("  gaussian - Use Gaussian weighted average (default)");
        Console.WriteLine();
        Console.WriteLine("Available layouts:");
        Console.WriteLine("  maxrects - MaxRects bin packing algorithm (default, better space utilization)");
        Console.WriteLine("  simple   - Simple row-based layout");
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

    if (args.Length > 5)
        useMaxRectsLayout = args[5].ToLower() switch
        {
            "simple" => false,
            _ => true,
        };

    return true;
}