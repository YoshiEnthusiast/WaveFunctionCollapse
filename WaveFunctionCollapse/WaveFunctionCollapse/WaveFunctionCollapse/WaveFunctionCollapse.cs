using System;
using System.Collections.Generic;
using System.Linq;

namespace WaveFunctionCollapseAlgorithm
{
    public sealed class WaveFunctionCollapse<T>
    {
        private readonly List<Pattern> _patterns = new List<Pattern>();
        private readonly List<T> _templateValues = new List<T>();
        private readonly bool[,,] _adjacencyRules;

        private readonly IEnumerable<int[]> _directions;
        private readonly IEnumerable<int[]> _cellOffsets;
        private readonly IEnumerable<int[]> _cellReflectionOffsets;

        private readonly int _tileSize;
        private readonly int _dimensions;
        private readonly int[] _multidimensionalTileSize;
        private readonly int _rotationSupportDimensions = 2;

        private Array _cells;
        private Random _random;

        private IEnumerable<int[]> _cellPositions;
        private IEnumerable<int> _periodicOutputDimensions;
        private int[] _outputSize;

        private int _collapsedSoFar;
        private bool _prepared;

        public WaveFunctionCollapse(Array template, int dimensions, int tileSize, WaveFunctionCollapseOptions options)
        {
            _dimensions = dimensions;
            _tileSize = tileSize;

            _multidimensionalTileSize = _dimensions.Enumerate().Select(item => _tileSize).ToArray();

            _cellOffsets = GetCellOffsets(_dimensions);
            _cellReflectionOffsets = GetCellOffsets(_dimensions - 1);

            _directions = GetAllDirections();

            int[] templateSize = GetTemplateSize(template).ToArray();

            IEnumerable<int[]> templateValuesPositions = templateSize.GetCartesianProductRange();
            IEnumerable<int> reflectedDimensions = options.ReflectedDimensions;            
                                                                                       
            foreach (int[] position in templateValuesPositions)                              
            {
                if (IsInvalidPosition(position, templateSize, options.PeriodicInputDimensions, _tileSize))
                    continue;

                Array tile = GetTile(position, template, templateSize);

                RegisterPattern(tile);

                if (options.Rotation && _dimensions == _rotationSupportDimensions)
                {
                    int[,] rotatedTile = tile as int[,];

                    for (int i = 0; i < 3; i++)
                    {
                        rotatedTile = RotateTile(rotatedTile);

                        RegisterPattern(rotatedTile);
                    }
                }

                for (int i = 0; i < _dimensions; i++)
                {
                    if (reflectedDimensions is not null && !reflectedDimensions.Contains(i))
                        continue;

                    Array reflectedTile = ReflectTile(tile, i);

                    RegisterPattern(reflectedTile);
                }
            }

            int patternsCount = _patterns.Count;
            int directionsCount = _directions.Count();
            _adjacencyRules = new bool[patternsCount, patternsCount, directionsCount];

            for (int i = 0; i < patternsCount; i++)
            {
                for (int j = 0; j < patternsCount; j++)
                {
                    Pattern pattern = _patterns[i];
                    Pattern other = _patterns[j];

                    for (int k = 0; k < _directions.Count(); k++)
                        _adjacencyRules[i, j, k] = pattern.Overlaps(other, _directions.ElementAt(k));
                }
            }
        }

        public WaveFunctionCollapse(Array template, int dimensions, int tileSize) : this(template, dimensions, tileSize, WaveFunctionCollapseOptions.Default)
        {

        }

        public bool IsFullyCollapsed => _cellPositions.Count() == _collapsedSoFar;
        public IEnumerable<T> TemplateValues => _templateValues;

        public Array Run(int[] outputSize, int? seed, IEnumerable<int> periodicOutputDimensions, out int contradictionsCount)
        {
            Prepare(outputSize, seed, periodicOutputDimensions);

            int totalContradictions = 0;

            while (!IsFullyCollapsed)
                totalContradictions += IterateUntilSuccess(out _);

            contradictionsCount = totalContradictions;  

            return GetCollapsedValues();
        }

        public void Prepare(int[] outputSize, int? seed, IEnumerable<int> periodicOutputDimensions)
        {
            _outputSize = outputSize;
            _periodicOutputDimensions = periodicOutputDimensions;
            _collapsedSoFar = 0;
            _prepared = true;
            _random = seed is null ? new Random() : new Random(seed.Value);

            _cellPositions = _outputSize.GetCartesianProductRange();
            _cells = CreateCellsArray();

            foreach (int[] position in _cellPositions)
            {
                var cell = new Cell(_patterns, position);

                _cells.SetValue(cell, position);
            }
        }

        public Array GetCollapsedValues()
        {
            if (!_prepared)
                return null;

            Array result = Array.CreateInstance(typeof(T), _outputSize);

            foreach (Cell cell in _cells)
            {
                if (cell.TryGetCollapsedIndex(out int index))
                {
                    T value = GetTemplateValue(index);

                    result.SetValue(value, cell.GetPositionAsArray());
                }
            }

            return result;
        }

        public Array GetPossibleValues()
        {
            if (!_prepared)
                return null;

            Array result = Array.CreateInstance(typeof(IEnumerable<T>), _outputSize); 

            foreach (Cell cell in _cells)
            {
                IEnumerable<T> values = GetPossibleCellValues(cell);

                result.SetValue(values, cell.GetPositionAsArray());
            }

            return result;
        }

        public IEnumerable<T> GetAllPossibleValues()
        {
            return _patterns.Select(pattern => _templateValues[pattern.FirstIndex]);
        }

        public bool Iterate(out IEnumerable<Element<T>> changedValues)
        {
            if (!_prepared)
            {
                changedValues = default(IEnumerable<Element<T>>);
                return false;
            }
            else if (IsFullyCollapsed)
            {
                changedValues = default(IEnumerable<Element<T>>);
                return true;
            }

            int[] position = GetLowestEntropyPosition();

            int collapsedPatternIndex = Collapse(position, out bool success, out IEnumerable<Cell> affectedCells);

            if (!success)
            {
                Cell cell = GetCell(position);

                cell.Constrain(collapsedPatternIndex, _patterns);

                if (cell.Collapsed)
                    _collapsedSoFar++;
            }

            changedValues = affectedCells is null ? default(IEnumerable<Element<T>>) : affectedCells.Select(cell => new Element<T>(GetPossibleCellValues(cell), cell.Position));
            return success;
        }

        public int IterateUntilSuccess(out IEnumerable<Element<T>> changedValues)
        {
            int contradictionsCount = 0;

            if (!_prepared || IsFullyCollapsed)
            {
                changedValues = default(IEnumerable<Element<T>>);
                return default(int);
            }

            while (true)
            {
                if (Iterate(out IEnumerable<Element<T>> affectedValues))
                {
                    changedValues = affectedValues;

                    return contradictionsCount;
                }

                contradictionsCount++;
            }
        }

        private int Collapse(int[] position, out bool success, out IEnumerable<Cell> affectedCells)
        {
            Cell cell = GetCell(position);

            IEnumerable<Pattern> possiblePatterns = GetPossiblePatterns(cell);

            int totalWeight = possiblePatterns.Sum(pattern => pattern.Weight);
            double label = _random.NextDouble() * totalWeight;

            foreach (Pattern pattern in possiblePatterns)
            {
                totalWeight -= pattern.Weight;

                if (totalWeight <= label)
                {
                    Array copy = CopyCells();
                    int patternIndex = _patterns.IndexOf(pattern);

                    cell.Collapse(patternIndex);

                    if (Propagate(cell, out int collapsedCount, out HashSet<Cell> changedCells))
                    {
                        changedCells.Add(cell);

                        success = true;
                        affectedCells = changedCells;
                        _collapsedSoFar += collapsedCount + 1;

                        return patternIndex;
                    }

                    success = false;

                    affectedCells = default(IEnumerable<Cell>);
                    _cells = copy;

                    return patternIndex;
                }
            }

            success = false;
            affectedCells = default(IEnumerable<Cell>);

            return default(int);
        }

        private bool Propagate(Cell cell, out int collapsedCount, out HashSet<Cell> affectedCells)
        {
            affectedCells = new HashSet<Cell>()
            {
                cell
            };

            var cellsToPropagate = new Stack<Cell>();
            collapsedCount = 0;

            cellsToPropagate.Push(cell);

            while (cellsToPropagate.Count > 0)
            {
                Cell currentCell = cellsToPropagate.Pop();
                int[] currentPosition = currentCell.GetPositionAsArray();
                IEnumerable<int> possibleIndices = currentCell.PossiblePatternsIndices;

                for (int i = 0; i < _directions.Count(); i++)
                {
                    int[] direction = _directions.ElementAt(i);

                    int[] neighbourPosition = currentPosition.Add(direction);

                    if (IsInvalidPosition(neighbourPosition, _outputSize, _periodicOutputDimensions))
                        continue;

                    for (int j = 0; j < neighbourPosition.Length; j++)
                    {
                        int unit = neighbourPosition[j];
                        int dimensionLenth = _outputSize[j];

                        if (unit < 0)
                            neighbourPosition[j] = dimensionLenth - 1;
                        else if (unit >= dimensionLenth)
                            neighbourPosition[j] = 0;
                    }

                    Cell neighbour = GetCell(neighbourPosition);
                    affectedCells.Add(neighbour);

                    if (neighbour.Collapsed || neighbour == currentCell)
                        continue;

                    IEnumerable<int> possibleNeighbourIndices = neighbour.PossiblePatternsIndices;
                    IEnumerable<int> incompatibleIndices = GetIncompatiblePatternsIndices(possibleNeighbourIndices, possibleIndices, i);

                    int neighbourIndicesCount = possibleNeighbourIndices.Count();
                    int incompatibleIndicesCount = incompatibleIndices.Count();

                    if (incompatibleIndicesCount < 1)
                        continue;

                    if (neighbourIndicesCount - incompatibleIndicesCount < 1)
                    {
                        return false;
                    }
                    else
                    {
                        neighbour.Constrain(incompatibleIndices.ToArray(), _patterns);

                        if (neighbour.Collapsed)
                            collapsedCount++;

                        cellsToPropagate.Push(neighbour);
                    }
                }
            }

            return true;
        }

        private IEnumerable<int> GetIncompatiblePatternsIndices(IEnumerable<int> patternIndices, IEnumerable<int> otherIndices, int directionIndex)
        {
            foreach (int index in patternIndices)
                if (!IsCompatibleWith(index, otherIndices, directionIndex))
                    yield return index;
        }

        private bool IsCompatibleWith(int patternIndex, IEnumerable<int> basePatternsIndices, int directionIndex)
        {
            foreach (int baseIndex in basePatternsIndices)
                if (_adjacencyRules[patternIndex, baseIndex, directionIndex])
                    return true;

            return false;
        }

        private int[] GetLowestEntropyPosition() 
        {
            double? lowestEntropy = null;
            int[] position = null;

            foreach (int[] cellPosition in _cellPositions)
            {
                Cell cell = GetCell(cellPosition);

                if (cell.Collapsed)
                    continue;

                double entropy = cell.Entropy;

                if (lowestEntropy is null || entropy < lowestEntropy.Value)
                {
                    lowestEntropy = entropy;
                    position = cellPosition;
                }
            }

            return position;
        }

        private IEnumerable<Pattern> GetPossiblePatterns(Cell cell)
        {
            foreach (int index in cell.PossiblePatternsIndices)
                yield return _patterns[index];
        }

        private Cell GetCell(int[] position)
        {
            return _cells.GetValue(position) as Cell;
        }

        private Array CopyCells()
        {
            Array copy = CreateCellsArray();

            foreach (Cell cell in _cells)
            {
                Cell clone = cell.Clone();

                copy.SetValue(clone, cell.GetPositionAsArray());
            }

            return copy;
        }

        private T GetTemplateValue(int patternIndex)
        {
            Pattern pattern = _patterns[patternIndex];

            return _templateValues[pattern.FirstIndex];
        }

        private IEnumerable<T> GetPossibleCellValues(Cell cell)
        {
            return cell.PossiblePatternsIndices.Select(index => GetTemplateValue(index));
        }

        private void RegisterPattern(Array tile)
        {
            foreach (Pattern pattern in _patterns)
            {
                if (pattern.Matches(tile))
                {
                    pattern.IncreaseWeight();

                    return;
                }
            }

            _patterns.Add(new Pattern(tile, _multidimensionalTileSize, _cellOffsets));
        }

        private Array GetTile(int[] startingPoint, Array template, int[] size)
        {
            Array result = CreateEmptyTile();

            foreach (int[] offset in _cellOffsets)
            {
                int[] position = startingPoint.Add(offset);

                for (int i = 0; i < position.Length; i++)
                {
                    int unit = position[i];
                    int dimensionLenth = size[i];

                    if (unit < 0)
                        position[i] = dimensionLenth + unit;
                    else if (unit >= dimensionLenth)
                        position[i] = unit - dimensionLenth;
                }

                int index = GetTemplateValueIndex(position, template);

                result.SetValue(index, offset);
            }

            return result;
        }

        private int GetTemplateValueIndex(int[] position, Array template)
        {
            T value = (T)template.GetValue(position);
            int count = _templateValues.Count;

            for (int i = 0; i < count; i++)
                if (_templateValues[i].Equals(value))
                    return i;

            _templateValues.Add(value);
            return count;
        }

        private Array ReflectTile(Array tile, int dimension)
        {
            Array result = CreateEmptyTile();

            foreach (int[] position in _cellReflectionOffsets)
            {
                for (int i = 0; i < Math.Ceiling(_tileSize / 2f); i++)
                {
                    int[] left = position.InsertUnit(i, dimension);
                    int[] right = position.InsertUnit(_tileSize - i - 1, dimension);

                    result.SetValue(tile.GetValue(left), right);
                    result.SetValue(tile.GetValue(right), left);
                }
            }

            return result;
        }

        private int[,] RotateTile(int[,] tile)
        {
            int[,] result = new int[_tileSize, _tileSize];

            for (int x = 0; x < _tileSize; x++)
            {
                for (int y = x; y < _tileSize; y++)
                {
                    result[x, y] = tile[y, x];
                    result[y, x] = tile[x, y];
                }
            }

            return ReflectTile(result, 0) as int[,];
        }

        private IEnumerable<int> GetTemplateSize(Array template)
        {
            for (int i = 0; i < _dimensions; i++)
                yield return template.GetLength(i);
        }

        private IEnumerable<int[]> GetCellOffsets(int dimensions)
        {
            return GetTileEdgePosition(dimensions).GetCartesianProductRange();
        }

        private IEnumerable<int> GetTileEdgePosition(int dimensions)
        {
            for (int i = 0; i < dimensions; i++)
                yield return _tileSize;
        }

        private bool IsInvalidPosition(int[] position, int[] size, IEnumerable<int> ignoredDimensions, int offset = 0)
        {
            for (int i = 0; i < position.Length; i++)
            {
                int unit = position[i];

                if (ignoredDimensions is null || ignoredDimensions.Contains(i))
                    continue;

                if (unit < 0 || unit >= size[i] || (offset > 0 && unit + offset > size[i]))
                    return true;
            }

            return false;
        }

        private IEnumerable<IEnumerable<int>> GetUnitsRanges()
        {
            for (int i = 0; i < _dimensions; i++)
                yield return Enumerable.Range(-1, 3).ToArray();
        }

        private bool IsValidDirection(IEnumerable<int> direction)
        {
            return direction.Select(unit => Math.Abs(unit)).Distinct().Count() == direction.Count();
        }

        private IEnumerable<int[]> GetAllDirections()
        {
            foreach (IEnumerable<int> direction in GetUnitsRanges().GetCartesianProduct())
                if (IsValidDirection(direction))
                    yield return direction.ToArray();
        }

        private Array CreateCellsArray()
        {
            return Array.CreateInstance(typeof(Cell), _outputSize);
        }

        private Array CreateEmptyTile()
        {
            return Array.CreateInstance(typeof(int), _multidimensionalTileSize);
        }
    }
}
