using System;
using System.Collections.Generic;
using System.Linq;

namespace WaveFunctionCollapseAlgorithm
{
    internal static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> GetCartesianProduct<T>(this IEnumerable<IEnumerable<T>> items)
        {
            IEnumerable<IEnumerator<T>> slots = items.Select(item => item.GetEnumerator()).Where(enumerator => enumerator.MoveNext()).ToArray();

            while (true)
            {
                yield return slots.Select(slot => slot.Current);

                foreach (IEnumerator<T> slot in slots)
                {
                    if (slot.MoveNext())
                    {
                        break;
                    }
                    else if (slot == slots.Last())
                    {
                        slot.Prepare();

                        foreach (IEnumerator<T> enumerator in slots)
                            enumerator.Dispose();

                        yield break;
                    }

                    slot.Prepare();
                }
            }
        }

        public static IEnumerable<int[]> GetCartesianProductRange(this IEnumerable<int> items)
        {
            return items.Select(item => Enumerable.Range(0, item).ToArray()).GetCartesianProduct().Select(item => item.ToArray());
        }

        public static void Prepare<T>(this IEnumerator<T> enumerator)
        {
            enumerator.Reset();
            enumerator.MoveNext();
        }

        public static IEnumerable<int> Enumerate(this int number)
        {
            return Enumerable.Range(0, number);
        }

        public static int[] Add(this int[] array, int[] other)
        {
            int length = array.Length;
            int[] result = new int[length]; 

            int minimalLength = Math.Min(length, other.Length);

            for (int i = 0; i < minimalLength; i++)
                result[i] = array[i] + other[i];

            return result;
        }

        public static int[] InsertUnit(this int[] position, int unit, int dimension)
        {
            List<int> result = position.ToList();
            result.Insert(dimension, unit);

            return result.ToArray();
        }
    }
}
