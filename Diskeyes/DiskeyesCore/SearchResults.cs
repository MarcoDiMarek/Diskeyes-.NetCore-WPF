using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace DiskeyesCore
{
    class SearchResults<T, K> where K : ISearchEntry<T>, new()
    {
        public delegate void ResultsOrderedHandler(KeyValuePair<int, K>[] results);
        public delegate void ResultsSealedHandler(KeyValuePair<int, K>[] orderedResults);
        public event ResultsOrderedHandler ResultsOrdered;
        public event ResultsSealedHandler ResultsSealed;
        public ReadOnlyDictionary<int, K> Results { get; private set; }
        public readonly int CompletionKey;
        private ConcurrentDictionary<int, K> results;
        const float MaxCountThreshold = 1.5f;
        private int MaxCount;
        private int MaxFinalCount;
        private int PartialCount;
        public SearchResults(int partialCount = 10, int maxCount = int.MaxValue / 2, int maxFinalCount = int.MaxValue - 1)
        {
            results = new ConcurrentDictionary<int, K>();
            MaxCount = maxCount;
            MaxFinalCount = Math.Clamp(maxFinalCount, 1, int.MaxValue - 1);
            CompletionKey = int.MaxValue;
            PartialCount = Math.Abs(partialCount);
        }
        public Dictionary<int, K> Peek(IEnumerable<int> indices)
        {
            var output = new Dictionary<int, K>();
            foreach(var index in indices)
            {
                K value;
                if (results.TryGetValue(index, out value))
                {
                    output[index] = value;
                }
            }
            return output;
        }
        public void Add(int index, T category, bool[] presence)
        {
            K entry;
            if (results.TryGetValue(index, out entry))
            {
                entry.Update(category, presence);
            }
            else
            {
                entry = new K();
                entry.Update(category, presence);
                results.TryAdd(index, entry);
                if (results.Count > MaxCount * MaxCountThreshold)
                {
                    Trim(MaxCount);
                }
            }
        }

        public void Add(int index, T category, string value)
        {
            K entry;
            if (results.TryGetValue(index, out entry))
            {
                entry.Update(category, value);
            }
            else
            {
                entry = new K();
                entry.Update(category, value);
                results.TryAdd(index, entry);
                if (results.Count > MaxCount * MaxCountThreshold)
                {
                    Trim(MaxCount);
                }
            }
        }

        public void Trim(int max)
        {
            if (results.Count > max)
            {
                var bestResults = results.ToArray().OrderByDescending(x => x.Value.GetScore()).Take(max).ToArray();
                results = new ConcurrentDictionary<int, K>(bestResults);
                int partialToTake = Math.Min(bestResults.Length, PartialCount);
                var partialResults = bestResults.Take(partialToTake);
                ResultsOrdered?.Invoke(partialResults.ToArray());
                Console.WriteLine("Trimmed size to " + results.Count.ToString());
            }
        }
        public async void Seal()
        {
            results.TryAdd(CompletionKey, new K());

            K found;
            while (!results.TryGetValue(CompletionKey, out found))
            {
                await Task.Delay(50);
            }
            IEnumerable<KeyValuePair<int,K>> bestResults;
            lock (results)
            {
                int toTake = Math.Min(MaxFinalCount, results.Count) - 1; // -1 to exclude the Completion Key
                bestResults = results.ToArray().OrderByDescending(x => x.Value.GetScore()).Take(toTake);
                results.Clear();
            }
            var dict = new Dictionary<int, K>(bestResults);
            Results = new ReadOnlyDictionary<int, K>(dict);
            ResultsSealed?.Invoke(bestResults.ToArray());
        }
    }
}
