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
        public ReadOnlyDictionary<int, K> Results { get { return finalResults; } }
        public readonly int CompletionKey;
        private ConcurrentDictionary<int, K> results;
        private ReadOnlyDictionary<int, K> finalResults;
        const float MaxCountThreshold = 1.5f;
        private int MaxCount;
        private int MaxFinalCount;
        public SearchResults(int maxCount = int.MaxValue / 2, int maxFinalCount = int.MaxValue - 1)
        {
            results = new ConcurrentDictionary<int, K>();
            MaxCount = maxCount;
            MaxFinalCount = Math.Clamp(maxFinalCount, 1, int.MaxValue - 1);
            CompletionKey = int.MaxValue;
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
        public void Trim(int max)
        {
            if (results.Count > max)
            {
                var bestResults = results.ToArray().OrderByDescending(x => x.Value.GetScore()).Take(max).ToArray();
                results = new ConcurrentDictionary<int, K>(bestResults);
                ResultsOrdered?.Invoke(bestResults);
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
            finalResults = new ReadOnlyDictionary<int, K>(dict);
            ResultsSealed?.Invoke(bestResults.ToArray());
            GC.Collect(2, GCCollectionMode.Optimized);
        }
    }
}
