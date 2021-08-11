using System;
using System.Collections.Generic;
using System.Linq;

namespace DiskeyesCore
{
    static class Utilities
    {
        /// <summary>
        /// Converts datatype of dictionary values.
        /// </summary>
        /// <typeparam name="K">Dictionary key data type of both original and converted dictionary.</typeparam>
        /// <typeparam name="V">Dictionary value data type of the original dictionary.</typeparam>
        /// <typeparam name="T">Target data type</typeparam>
        /// <param name="originalDict">Original dictionary</param>
        /// <param name="convertor">A Lambda convertor between the original dictionary's key type and the target data type.</param>
        public static Dictionary<K, Z> ConvertDict<K, V, Z>(Dictionary<K, V> originalDict, Func<V, Z> convertor)
        {
            var converted = new Dictionary<K, Z>(originalDict.Count);
            foreach (KeyValuePair<K, V> original in originalDict)
            {
                converted[original.Key] = convertor(original.Value);
            }
            return converted;
        }

        /// <typeparam name="K">Value type of the dictionary</typeparam>
        /// <returns>The highest key of a dictionary or -1</returns>
        public static int MaxKey<K>(ref Dictionary<int, K> dict)
        {
            try
            {
                return dict.Keys.Max();
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        public static Dictionary<K, V> MergeDictionaries<K, V>(Dictionary<K, V> first, Dictionary<K, V> overwrite)
        {
            var merged = new Dictionary<K, V>(first);
            foreach (K index in overwrite.Keys)
            {
                merged[index] = overwrite[index];
            }
            return merged;
        }

        public static T[] ConvertEach<T, U>(U[] elements, Func<U, T> convertor)
        {
            var output = new T[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                output[i] = convertor(elements[i]);
            return output;
        }
    }
}
