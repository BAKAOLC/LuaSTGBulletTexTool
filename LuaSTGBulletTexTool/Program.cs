using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TexCombineTool;
using TexCombineTool.Helpers;
using TexCombineTool.Layout;
using TexCombineTool.Models;
using TexCombineTool.Services;

var startTime = DateTime.Now;

if (!ValidateAndParseArgs(args, out var jsonFilePath, out var mode, out var width, out var height, out var margin,
        out var algorithm, out var useMaxRects))
    return;

var outputDirectory = Path.Combine(Path.GetDirectoryName(jsonFilePath) ?? string.Empty, "generated");
if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

switch (mode)
{
    case ProcessMode.SplitOnly:
        await ProcessSplitOnlyMode(jsonFilePath, outputDirectory).ConfigureAwait(false);
        break;
    case ProcessMode.CombineOnly:
        await ProcessCombineOnlyMode(outputDirectory, width, height, margin, algorithm, useMaxRects)
            .ConfigureAwait(false);
        break;
    case ProcessMode.CombineWithSplit:
        await ProcessCombineWithSplitMode(jsonFilePath, outputDirectory, width, height, margin, algorithm,
            useMaxRects).ConfigureAwait(false);
        break;
}

Console.WriteLine($"Total time: {DateTime.Now - startTime}");
return;

async Task ProcessSplitOnlyMode(string jsonPath, string outputDir)
{
    Console.WriteLine("Mode: Split only - generating individual sprite images and JSON");

    var bulletData = FileHelper.LoadBulletData(jsonPath);
    if (bulletData == null)
    {
        Console.WriteLine("Failed to load bullet data.");
        return;
    }

    var texturePool = FileHelper.LoadTextures(bulletData, Path.GetDirectoryName(jsonPath) ?? string.Empty);
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

    OutputService.SaveSpritePools(textureSpritePool, outputDir);

    var (combinedSpritePool, combinedSameSpritePool) =
        SpriteLoader.CombineSpritePools(textureSpritePool, sameSpritePool);
    var spriteDataMap = SpriteLoader.MapSpriteData(bulletData.Sprites, sameSpritePool);
    var cropRangePool = textureSpriteCropRangePool.SelectMany(x => x.Value)
        .ToDictionary(x => x.Key, x => x.Value);

    await OutputService.SaveSplitJson(outputDir, bulletData, combinedSpritePool, spriteDataMap,
        combinedSameSpritePool, cropRangePool).ConfigureAwait(false);

    Console.WriteLine("Completed split mode");
}

async Task ProcessCombineOnlyMode(string outputDir, int texWidth, int texHeight, int spriteMargin,
    AlphaColorFixAlgorithm algorithmType, bool useMaxRectsLayout)
{
    Console.WriteLine("Mode: Combine only - loading split sprites and generating atlas");

    var splitJsonPath = Path.Combine(outputDir, "bullet_split.json");
    if (!File.Exists(splitJsonPath))
    {
        Console.WriteLine($"Split JSON not found: {splitJsonPath}");
        Console.WriteLine("Please run in 'split' mode first to generate split sprites.");
        return;
    }

    var splitBulletData = FileHelper.LoadBulletData(splitJsonPath);
    if (splitBulletData == null)
    {
        Console.WriteLine("Failed to load split bullet data.");
        return;
    }

    var splitTexturePool = FileHelper.LoadTextures(splitBulletData, outputDir);
    if (splitTexturePool.Count == 0)
    {
        Console.WriteLine("No split textures loaded. Exiting.");
        return;
    }

    Console.WriteLine($"Loaded {splitTexturePool.Count} split textures.");

    var spritePool = new Dictionary<string, Image<Rgba32>>();
    var spriteDataMap = new Dictionary<string, SpriteData>();
    var sameSpritePool = new Dictionary<string, string[]>();
    var cropRangePool = new Dictionary<string, CropRange>();

    var textureToSprites = new Dictionary<string, List<SpriteData>>();
    foreach (var sprite in splitBulletData.Sprites)
    {
        if (!textureToSprites.ContainsKey(sprite.Texture))
            textureToSprites[sprite.Texture] = new();
        textureToSprites[sprite.Texture].Add(sprite);
    }

    foreach (var (textureName, sprites) in textureToSprites)
    {
        if (!splitTexturePool.TryGetValue(textureName, out var texture))
            continue;

        var mainSprite = sprites[0];
        spritePool[mainSprite.Name] = texture;
        spriteDataMap[mainSprite.Name] = mainSprite;

        if (sprites.Count > 1)
        {
            var sameSpriteNames = sprites.Skip(1).Select(s => s.Name).ToArray();
            sameSpritePool[mainSprite.Name] = sameSpriteNames;

            foreach (var sameSprite in sprites.Skip(1))
                spriteDataMap[sameSprite.Name] = sameSprite;
        }
    }

    Console.WriteLine($"Loaded {spritePool.Count} unique sprites for combining.");

    ILayoutAlgorithm layoutAlgorithm = useMaxRectsLayout
        ? new MaxRectsLayoutAlgorithm()
        : new SimpleRowLayoutAlgorithm();

    Console.WriteLine($"Using layout algorithm: {layoutAlgorithm.GetType().Name}");

    var spriteCombiner = new SpriteCombiner(layoutAlgorithm);
    var (combinedSprite, colorMapSprite, spriteDataList) =
        await spriteCombiner.CombineSprites("bullet_atlas", texWidth, texHeight,
                spritePool, spriteDataMap, sameSpritePool, cropRangePool,
                spriteMargin, algorithmType)
            .ConfigureAwait(false);

    await OutputService.SaveResults(outputDir, combinedSprite, colorMapSprite, splitBulletData, spriteDataList)
        .ConfigureAwait(false);

    Console.WriteLine("Completed combine mode");
}

async Task ProcessCombineWithSplitMode(string jsonPath, string outputDir, int texWidth, int texHeight,
    int spriteMargin, AlphaColorFixAlgorithm algorithmType, bool useMaxRectsLayout)
{
    Console.WriteLine("Mode: Both - generating split sprites and atlas");

    var bulletData = FileHelper.LoadBulletData(jsonPath);
    if (bulletData == null)
    {
        Console.WriteLine("Failed to load bullet data.");
        return;
    }

    var texturePool = FileHelper.LoadTextures(bulletData, Path.GetDirectoryName(jsonPath) ?? string.Empty);
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

    OutputService.SaveSpritePools(textureSpritePool, outputDir);

    var (combinedSpritePool, combinedSameSpritePool) =
        SpriteLoader.CombineSpritePools(textureSpritePool, sameSpritePool);
    var spriteDataMap = SpriteLoader.MapSpriteData(bulletData.Sprites, sameSpritePool);
    var cropRangePool = textureSpriteCropRangePool.SelectMany(x => x.Value)
        .ToDictionary(x => x.Key, x => x.Value);

    await OutputService.SaveSplitJson(outputDir, bulletData, combinedSpritePool, spriteDataMap,
        combinedSameSpritePool, cropRangePool).ConfigureAwait(false);

    ILayoutAlgorithm layoutAlgorithm = useMaxRectsLayout
        ? new MaxRectsLayoutAlgorithm()
        : new SimpleRowLayoutAlgorithm();

    Console.WriteLine($"Using layout algorithm: {layoutAlgorithm.GetType().Name}");

    var spriteCombiner = new SpriteCombiner(layoutAlgorithm);
    var (combinedSprite, colorMapSprite, spriteDataList) =
        await spriteCombiner.CombineSprites("bullet_atlas", texWidth, texHeight,
                combinedSpritePool, spriteDataMap, combinedSameSpritePool, cropRangePool,
                spriteMargin, algorithmType)
            .ConfigureAwait(false);

    await OutputService.SaveResults(outputDir, combinedSprite, colorMapSprite, bulletData, spriteDataList)
        .ConfigureAwait(false);

    Console.WriteLine("Completed both modes");
}

bool ValidateAndParseArgs(string[] args, out string path, out ProcessMode processMode, out int texWidth,
    out int texHeight, out int spriteMargin,
    out AlphaColorFixAlgorithm algorithmType, out bool useMaxRectsLayout)
{
    path = string.Empty;
    processMode = ProcessMode.CombineWithSplit;
    texWidth = 0;
    texHeight = 0;
    spriteMargin = 4;
    algorithmType = AlphaColorFixAlgorithm.Gaussian;
    useMaxRectsLayout = true;

    if (args.Length < 1)
    {
        Console.WriteLine("Usage: <json> [mode] [width] [height] [margin] [algorithm] [layout]");
        Console.WriteLine();
        Console.WriteLine("Available modes:");
        Console.WriteLine("  split    - Only generate split sprites and JSON (no atlas generation)");
        Console.WriteLine("  combine  - Generate atlas from split sprites JSON");
        Console.WriteLine("  both     - Generate both split sprites and atlas (default)");
        Console.WriteLine();
        Console.WriteLine("For 'split' mode: <json> split");
        Console.WriteLine("For 'combine' mode: <json> combine <width> <height> [margin] [algorithm] [layout]");
        Console.WriteLine("For 'both' mode: <json> both <width> <height> [margin] [algorithm] [layout]");
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

    var modeArgIndex = 1;
    if (args.Length > 1)
    {
        processMode = args[1].ToLower() switch
        {
            "split" => ProcessMode.SplitOnly,
            "combine" => ProcessMode.CombineOnly,
            _ => ProcessMode.CombineWithSplit,
        };

        if (int.TryParse(args[1], out _))
        {
            processMode = ProcessMode.CombineWithSplit;
            modeArgIndex = 0;
        }
    }

    if (processMode == ProcessMode.SplitOnly) return true;

    var widthArgIndex = modeArgIndex + 1;
    var heightArgIndex = modeArgIndex + 2;

    if (args.Length < heightArgIndex + 1)
    {
        Console.WriteLine($"Mode '{processMode}' requires width and height parameters.");
        return false;
    }

    if (!int.TryParse(args[widthArgIndex], out texWidth) || !int.TryParse(args[heightArgIndex], out texHeight))
    {
        Console.WriteLine("Invalid width or height.");
        return false;
    }

    if (texWidth <= 0 || texHeight <= 0)
    {
        Console.WriteLine("Width and height must be greater than 0.");
        return false;
    }

    var marginArgIndex = heightArgIndex + 1;
    if (args.Length > marginArgIndex && int.TryParse(args[marginArgIndex], out var marginValue))
    {
        if (marginValue < 0)
        {
            Console.WriteLine("Margin must be greater than or equal to 0.");
            return false;
        }

        spriteMargin = marginValue;
    }

    var algorithmArgIndex = marginArgIndex + 1;
    if (args.Length > algorithmArgIndex)
        algorithmType = args[algorithmArgIndex].ToLower() switch
        {
            "none" => AlphaColorFixAlgorithm.None,
            "nearest" => AlphaColorFixAlgorithm.Nearest,
            "weighted" => AlphaColorFixAlgorithm.Weighted,
            _ => AlphaColorFixAlgorithm.Gaussian,
        };

    var layoutArgIndex = algorithmArgIndex + 1;
    if (args.Length > layoutArgIndex)
        useMaxRectsLayout = args[layoutArgIndex].ToLower() switch
        {
            "simple" => false,
            _ => true,
        };

    return true;
}

internal enum ProcessMode
{
    SplitOnly, // 只生成散图
    CombineOnly, // 只合并散图
    CombineWithSplit, // 生成散图并合并
}