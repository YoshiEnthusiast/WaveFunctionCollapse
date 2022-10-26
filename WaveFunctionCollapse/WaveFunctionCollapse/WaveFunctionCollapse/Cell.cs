using System;
using System.Collections.Generic;
using System.Linq;

namespace WaveFunctionCollapseAlgorithm
{
    internal sealed class Cell
    {
        private List<int> _possiblePatternsIndices;
        private IEnumerable<int> _position;

        private int _sumOfWeights;
        private double _sumOfWeightLogWeights;
        private double _entropy;

        private bool _collapsed;

        public Cell(IEnumerable<Pattern> possiblePatterns, IEnumerable<int> position)
        {
            _possiblePatternsIndices = possiblePatterns.Count().Enumerate().ToList();
            _position = position;

            foreach (Pattern pattern in possiblePatterns)
            {
                int weight = pattern.Weight;

                _sumOfWeights += weight;
                _sumOfWeightLogWeights += GetWeightLogWeight(weight);
            }

            UpdateEntropy();
        }

        private Cell(IEnumerable<int> possiblePatternsIndices, IEnumerable<int> position, int sumOfWeights, double sumOfWeightLogWeights, double entropy)
        {
            _possiblePatternsIndices = possiblePatternsIndices.ToList();
            _position = position;

            _sumOfWeights = sumOfWeights;
            _sumOfWeightLogWeights = sumOfWeightLogWeights;
            _entropy = entropy;
        }

        private Cell(int collapsedIndex, IEnumerable<int> position)
        {
            _possiblePatternsIndices = new List<int>(1)
            {
                collapsedIndex
            };

            _collapsed = true;
            _position = position;
        }

        public double Entropy => _entropy;
        public bool Collapsed => _collapsed;
        public IEnumerable<int> PossiblePatternsIndices => _possiblePatternsIndices;
        public IEnumerable<int> Position => _position;

        public void Collapse(int into)
        {
            if (_collapsed)
                return;

            _possiblePatternsIndices.RemoveAll(index => index != into);
            _collapsed = true;

            _sumOfWeights = 0;
            _sumOfWeightLogWeights = 0d;
            _entropy = 0d;
        }

        public void Constrain(IEnumerable<int> indices, IEnumerable<Pattern> allPatterns)
        {
            if (_collapsed)
                return;

            foreach (int index in indices)
            {
                if (_possiblePatternsIndices.Remove(index))
                {
                    Pattern pattern = allPatterns.ElementAt(index);
                    int weight = pattern.Weight;

                    _sumOfWeights -= weight;
                    _sumOfWeightLogWeights -= GetWeightLogWeight(weight);
                }
            }

            UpdateEntropy();

            _collapsed = _possiblePatternsIndices.Count == 1;
        }

        public void Constrain(int index, IEnumerable<Pattern> allPatterns)
        {
            var indices = new int[]
            {
                index
            };

            Constrain(indices, allPatterns);    
        }

        public bool TryGetCollapsedIndex(out int index)
        {
            if (_collapsed)
            {
                index = GetCollapsedIndex();

                return true;
            }

            index = default(int);

            return false;
        }

        public Cell Clone()
        {
            if (_collapsed)
                return new Cell(GetCollapsedIndex(), _position);

            return new Cell(_possiblePatternsIndices, _position, _sumOfWeights, _sumOfWeightLogWeights, _entropy);
        }

        public int[] GetPositionAsArray()
        {
            return _position.ToArray();
        }

        private void UpdateEntropy()
        {
            _entropy = Math.Log(_sumOfWeights) - (_sumOfWeightLogWeights / _sumOfWeights);
        }

        private double GetWeightLogWeight(int weight)
        {
            return weight * Math.Log(weight);
        }

        private int GetCollapsedIndex()
        {
            return _possiblePatternsIndices.First();
        }
    }
}
