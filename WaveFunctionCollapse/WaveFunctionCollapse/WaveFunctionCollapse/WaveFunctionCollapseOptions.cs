using System.Collections.Generic;

namespace WaveFunctionCollapseAlgorithm
{
    public sealed class WaveFunctionCollapseOptions
    {
        public static readonly WaveFunctionCollapseOptions Default = new WaveFunctionCollapseOptions();

        public bool Rotation { get; init; }

        public IEnumerable<int> PeriodicInputDimensions { get; init; }

        public IEnumerable<int> ReflectedDimensions { get; init; }
    }
}
