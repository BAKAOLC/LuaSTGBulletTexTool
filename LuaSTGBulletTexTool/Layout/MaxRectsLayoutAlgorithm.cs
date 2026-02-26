using SixLabors.ImageSharp;

namespace TexCombineTool.Layout
{
    internal class MaxRectsLayoutAlgorithm : ILayoutAlgorithm
    {
        public List<(string name, Rectangle rect)> Layout(
            Dictionary<string, Size> sprites,
            int maxWidth,
            int maxHeight,
            int margin)
        {
            var methods = new[]
            {
                FreeRectChoiceHeuristic.BestShortSideFit,
                FreeRectChoiceHeuristic.BestLongSideFit,
                FreeRectChoiceHeuristic.BestAreaFit,
                FreeRectChoiceHeuristic.BottomLeftRule,
            };

            List<(string, Rectangle)>? bestResult = null;
            var bestCount = 0;
            var bestOccupancy = 0.0;
            FreeRectChoiceHeuristic? bestMethod = null;

            foreach (var method in methods)
            {
                var result = TryLayout(sprites, maxWidth, maxHeight, margin, method);
                var occupancy = CalculateOccupancy(result, maxWidth, maxHeight);

                if (result.Count <= bestCount && (result.Count != bestCount || !(occupancy > bestOccupancy))) continue;
                bestResult = result;
                bestCount = result.Count;
                bestOccupancy = occupancy;
                bestMethod = method;
            }

            Console.WriteLine(
                $"MaxRects layout completed using {bestMethod}: {bestCount}/{sprites.Count} sprites placed, occupancy: {bestOccupancy:P2}");
            return bestResult ?? [];
        }

        private static List<(string name, Rectangle rect)> TryLayout(
            Dictionary<string, Size> sprites,
            int maxWidth,
            int maxHeight,
            int margin,
            FreeRectChoiceHeuristic method)
        {
            var result = new List<(string, Rectangle)>();
            var packer = new MaxRectsPacker(maxWidth, maxHeight);

            var sortedSprites = sprites
                .OrderByDescending(x => x.Value.Width * x.Value.Height)
                .ThenByDescending(x => Math.Max(x.Value.Width, x.Value.Height))
                .ToList();

            foreach (var (name, size) in sortedSprites)
            {
                var paddedWidth = size.Width + margin * 2;
                var paddedHeight = size.Height + margin * 2;

                var rect = packer.Insert(paddedWidth, paddedHeight, method);

                if (rect == null)
                    continue;

                var finalRect = new Rectangle(
                    rect.Value.X + margin,
                    rect.Value.Y + margin,
                    size.Width,
                    size.Height);

                result.Add((name, finalRect));
            }

            return result;
        }

        private static double CalculateOccupancy(List<(string name, Rectangle rect)> layout, int width, int height)
        {
            if (layout.Count == 0) return 0;

            var totalArea = layout.Sum(x => x.rect.Width * x.rect.Height);
            var canvasArea = width * height;
            return (double)totalArea / canvasArea;
        }

        private enum FreeRectChoiceHeuristic
        {
            BestShortSideFit,
            BestLongSideFit,
            BestAreaFit,
            BottomLeftRule,
        }

        private class MaxRectsPacker
        {
            private readonly int _binHeight;
            private readonly int _binWidth;
            private readonly List<Rectangle> _freeRectangles = [];

            public MaxRectsPacker(int width, int height)
            {
                _binWidth = width;
                _binHeight = height;
                _freeRectangles.Add(new(0, 0, width, height));
            }

            public Rectangle? Insert(int width, int height, FreeRectChoiceHeuristic method)
            {
                Rectangle? bestNode = null;
                var bestScore1 = int.MaxValue;
                var bestScore2 = int.MaxValue;

                foreach (var freeRect in _freeRectangles)
                {
                    if (freeRect.Width < width || freeRect.Height < height)
                        continue;

                    var score1 = 0;
                    var score2 = 0;

                    switch (method)
                    {
                        case FreeRectChoiceHeuristic.BestShortSideFit:
                            score1 = Math.Min(freeRect.Width - width, freeRect.Height - height);
                            score2 = Math.Max(freeRect.Width - width, freeRect.Height - height);
                            break;
                        case FreeRectChoiceHeuristic.BestLongSideFit:
                            score1 = Math.Max(freeRect.Width - width, freeRect.Height - height);
                            score2 = Math.Min(freeRect.Width - width, freeRect.Height - height);
                            break;
                        case FreeRectChoiceHeuristic.BestAreaFit:
                            score1 = freeRect.Width * freeRect.Height - width * height;
                            score2 = Math.Min(freeRect.Width - width, freeRect.Height - height);
                            break;
                        case FreeRectChoiceHeuristic.BottomLeftRule:
                            score1 = freeRect.Y;
                            score2 = freeRect.X;
                            break;
                    }

                    if (score1 >= bestScore1 && (score1 != bestScore1 || score2 >= bestScore2)) continue;
                    bestNode = new Rectangle(freeRect.X, freeRect.Y, width, height);
                    bestScore1 = score1;
                    bestScore2 = score2;
                }

                if (bestNode == null)
                    return null;

                PlaceRectangle(bestNode.Value);
                return bestNode;
            }

            private void PlaceRectangle(Rectangle node)
            {
                var numRectanglesToProcess = _freeRectangles.Count;
                for (var i = 0; i < numRectanglesToProcess; i++)
                    if (SplitFreeNode(_freeRectangles[i], node))
                    {
                        _freeRectangles.RemoveAt(i);
                        --i;
                        --numRectanglesToProcess;
                    }

                PruneFreeList();
            }

            private bool SplitFreeNode(Rectangle freeNode, Rectangle usedNode)
            {
                if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
                    usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                    return false;

                if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
                {
                    if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                    {
                        var newNode = freeNode;
                        newNode.Height = usedNode.Y - newNode.Y;
                        _freeRectangles.Add(newNode);
                    }

                    if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                    {
                        var newNode = freeNode;
                        newNode.Y = usedNode.Y + usedNode.Height;
                        newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
                        _freeRectangles.Add(newNode);
                    }
                }

                if (usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                    return true;
                {
                    if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                    {
                        var newNode = freeNode;
                        newNode.Width = usedNode.X - newNode.X;
                        _freeRectangles.Add(newNode);
                    }

                    if (usedNode.X + usedNode.Width >= freeNode.X + freeNode.Width) return true;
                    {
                        var newNode = freeNode;
                        newNode.X = usedNode.X + usedNode.Width;
                        newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
                        _freeRectangles.Add(newNode);
                    }
                }

                return true;
            }

            private void PruneFreeList()
            {
                for (var i = 0; i < _freeRectangles.Count; i++)
                for (var j = i + 1; j < _freeRectangles.Count; j++)
                {
                    if (IsContainedIn(_freeRectangles[i], _freeRectangles[j]))
                    {
                        _freeRectangles.RemoveAt(i);
                        --i;
                        break;
                    }

                    if (!IsContainedIn(_freeRectangles[j], _freeRectangles[i])) continue;
                    _freeRectangles.RemoveAt(j);
                    --j;
                }
            }

            private static bool IsContainedIn(Rectangle a, Rectangle b)
            {
                return a.X >= b.X && a.Y >= b.Y &&
                       a.X + a.Width <= b.X + b.Width &&
                       a.Y + a.Height <= b.Y + b.Height;
            }
        }
    }
}