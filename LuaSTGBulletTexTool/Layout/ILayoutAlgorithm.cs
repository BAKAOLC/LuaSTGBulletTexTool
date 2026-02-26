using SixLabors.ImageSharp;

namespace TexCombineTool.Layout
{
    internal interface ILayoutAlgorithm
    {
        List<(string name, Rectangle rect)> Layout(
            Dictionary<string, Size> sprites,
            int maxWidth,
            int maxHeight,
            int margin);
    }
}