using System;
using System.Collections.Generic;
using System.Linq;

namespace WaveFunctionCollapseAlgorithm
{
    internal sealed class Pattern
    {
        private readonly Array _indices;
        private readonly IEnumerable<int[]> _positions;
        private readonly IEnumerable<int> _size;

        private readonly int _firstIndex;

        private int _weight = 1;

        public Pattern(Array values, IEnumerable<int> size, IEnumerable<IEnumerable<int>> positions) 
        {
            _size = size;
            _positions = positions.Select(position => position.ToArray());
            _firstIndex = (int)values.GetValue(size.Select(value => 0).ToArray());

            _indices = Array.CreateInstance(typeof(int), _size.ToArray());

            foreach (int[] position in _positions)
                _indices.SetValue(values.GetValue(position), position);
        }

        public IEnumerable<int> Size => _size;
        public int FirstIndex => _firstIndex;   
        public int Weight => _weight;

        public bool Overlaps(Pattern pattern, int[] offset)
        {
            int offsetDimensions = offset.Length;

            int dimensions = _size.Count();
            int otherDimensions = pattern.Size.Count();

            if (dimensions != otherDimensions || offsetDimensions > dimensions || offsetDimensions > otherDimensions)
                return false;

            for (int i = 0; i < offsetDimensions; i++)
                if (Math.Abs(offset[i]) >= _size.ElementAt(i))
                    return false;

            foreach (int[] position in _positions)
            {
                TryGetIndex(position, out int index);

                if (pattern.TryGetIndex(position.Add(offset), out int other) && index != other)
                    return false;
            }

            return true;
        }

        public bool TryGetIndex(int[] position, out int index)
        {
            for (int i = 0; i < position.Length; i++)
            {
                int unit = position[i];

                if (unit < 0 || unit >= _size.ElementAt(i))
                {
                    index = default(int);

                    return false;
                }
            }

            index = (int)_indices.GetValue(position);

            return true;
        }

        public bool Matches(Array indices)
        {
            foreach (int[] position in _positions)
                if (!_indices.GetValue(position).Equals(indices.GetValue(position)))
                    return false;

            return true;
        }

        public void IncreaseWeight()
        {
            _weight++;
        }
    }
}
