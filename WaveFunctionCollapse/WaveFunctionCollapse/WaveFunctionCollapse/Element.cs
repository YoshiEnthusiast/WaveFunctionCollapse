using System.Collections.Generic;

namespace WaveFunctionCollapseAlgorithm
{
    public sealed record class Element<T>(IEnumerable<T> Values, IEnumerable<int> Position);
}
