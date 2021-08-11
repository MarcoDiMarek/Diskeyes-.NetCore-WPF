using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;

namespace DiskeyesCore
{
    class SearchResults
    {
        public delegate void ResultsOrderedHandler(KeyValuePair<int, SearchEntry>[] results);
        public event ResultsOrderedHandler ResultsOrdered;
        public ReadOnlyDictionary<int, SearchEntry> Results { get { return new ReadOnlyDictionary<int, SearchEntry>(results); } }
        private ConcurrentDictionary<int, SearchEntry> results;
        const float MaxCountThreshold = 1.5f;
        private int MaxCount;
        private int MaxFinalCount;
        private int average = 0;
        public SearchResults(int maxCount = int.MaxValue, int maxFinalCount = int.MaxValue)
        {
            results = new ConcurrentDictionary<int, SearchEntry>();
            MaxCount = maxCount;
            MaxFinalCount = maxFinalCount;
        }
        public void Add(int index, SearchCategory category, bool[] presence)
        {
            SearchEntry entry;
            if (results.TryGetValue(index, out entry))
            {
                entry.Update(category, presence);
            }
            else
            {
                entry = new SearchEntry(index);
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
                var bestResults = results.ToArray().OrderByDescending(x => x.Value.Score).Take(max).ToArray();
                results = new ConcurrentDictionary<int, SearchEntry>(bestResults);
                ResultsOrdered?.Invoke(bestResults);
                Console.WriteLine("Trimmed size to " + results.Count.ToString());
            }
        }
        public void Seal()
        {
            lock (results) {
                if (results.Count > MaxFinalCount)
                {
                    var bestResults = results.ToArray().OrderByDescending(x => x.Value.Score).Take(MaxFinalCount);
                    results = new ConcurrentDictionary<int, SearchEntry>(bestResults);
                    GC.Collect();
                }
            }
        }
    }
}
