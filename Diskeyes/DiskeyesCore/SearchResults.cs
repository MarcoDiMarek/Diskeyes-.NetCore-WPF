using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;

namespace DiskeyesCore
{
    class SearchResults<T, K> where K : ISearchEntry<T>, new()
    {
        public delegate void ResultsOrderedHandler(KeyValuePair<int, K>[] results);
        public event ResultsOrderedHandler ResultsOrdered;
        public ReadOnlyDictionary<int, K> Results { get { return new ReadOnlyDictionary<int, K>(results); } }
        private ConcurrentDictionary<int, K> results;
        const float MaxCountThreshold = 1.5f;
        private int MaxCount;
        private int MaxFinalCount;
        public SearchResults(int maxCount = int.MaxValue, int maxFinalCount = int.MaxValue)
        {
            results = new ConcurrentDictionary<int, K>();
            MaxCount = maxCount;
            MaxFinalCount = maxFinalCount;
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
                if(results.Count > MaxCount * MaxCountThreshold)
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
        public void Seal()
        {
            lock (results) {
                if (results.Count > MaxFinalCount)
                {
                    var bestResults = results.ToArray().OrderByDescending(x => x.Value.GetScore()).Take(MaxFinalCount);
                    results = new ConcurrentDictionary<int, K>(bestResults);
                    GC.Collect();
                }
            }
        }
    }
}
