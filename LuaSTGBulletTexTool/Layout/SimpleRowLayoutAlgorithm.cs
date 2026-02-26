using System.Globalization;
using SixLabors.ImageSharp;

namespace TexCombineTool.Layout
{
    internal class SimpleRowLayoutAlgorithm : ILayoutAlgorithm
    {
        public List<(string name, Rectangle rect)> Layout(
            Dictionary<string, Size> sprites,
            int maxWidth,
            int maxHeight,
            int margin)
        {
            var result = new List<(string, Rectangle)>();
            var x = 0;
            var y = 0;
            var maxRowHeight = 0;

            foreach (var (name, size) in sprites.OrderByDescending(kv => kv.Value.Height)
                         .ThenBy(kv => kv.Key,
                             StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.NumericOrdering)))
            {
                if (x + size.Width + margin * 2 > maxWidth)
                {
                    x = 0;
                    y += maxRowHeight + margin * 2;
                    maxRowHeight = 0;
                }

                maxRowHeight = Math.Max(maxRowHeight, size.Height);
                if (y + maxRowHeight + margin * 2 > maxHeight)
                {
                    Console.WriteLine($"Not enough space for sprite: {name}");
                    break;
                }

                var rect = new Rectangle(x + margin, y + margin, size.Width, size.Height);
                result.Add((name, rect));
                x += size.Width + margin * 2;
            }

            return result;
        }
    }
}