﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DiskeyesCore
{
    /// <summary>
    /// A generic abstraction of MovieDB class. A Table manages a collection of LineDBColumns,
    /// allowing for more versatility of SearchEntry implementation and independent of the SearchCategory enum.
    /// </summary>
    /// <typeparam name="T">Search categories</typeparam>
    /// <typeparam name="K">Search entry type</typeparam>
    class Table<T, K>
        where T : Enum
        where K : ISearchEntry<T>, new()
    {
        public delegate void SearchFinishedHandler(SearchResults<T, K> results);
        public delegate void PartialResultsHandler(KeyValuePair<int, K>[] orderedBestResults);
        public delegate void TableInitializedHandler();
        public event SearchFinishedHandler SearchFinished;
        public event PartialResultsHandler PartialResultsSorted;
        public event TableInitializedHandler TableInitialized;
        private HashSet<int> available;
        private Dictionary<T, Column> fields;
        private CancellationTokenSource cancellationTokenSource;
        private List<Task> tasks;
        private SearchResults<T, K> results;
        public Table(Dictionary<T, Column> columns, bool initializeNow = false, bool blocking = false)
        {
            fields = columns;
            if (initializeNow)
            {
                if (blocking)
                {
                    Initialize().RunSynchronously();
                }
                else
                {
                    Task.Run(async () =>
                    {
                        await Initialize();
                    });
                }
            }
        }
        public async Task<bool> Initialize()
        {
            var initializations = from column in fields
                                  select column.Value.Initialize();

            await Task.WhenAll(initializations.ToArray());
            TableInitialized?.Invoke();
            return true;
        }

        public void ProgressReporter(SearchBatch additions)
        {
            int identifier = additions.CategoryIdentifier;
            //T category = (T)(object)identifier;
            T category = Unsafe.As<int, T>(ref identifier);
            foreach (var (index, presence) in additions.BoolValues)
            {
                results.Add(index, category, presence);
            }
            Console.WriteLine(string.Format("{0} found {1} matches", category, additions.BoolValues.Count));
        }
        public async Task<bool> Search(IQuery<T> query, int resultsRAM = 5000, int finalMaxCount = 100)
        {
            if (tasks != null && tasks.Count > 0)
                await CancelSearch();

            results = new SearchResults<T, K>(resultsRAM, finalMaxCount);
            results.ResultsOrdered += OnPartialResultsSorted;
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var progress = new Progress<SearchBatch>(ProgressReporter);
            tasks = new List<Task>();

            var queryData = query.GetQueryData();
            foreach (var searchField in queryData.Keys.Intersect(fields.Keys))
            {
                T field = searchField;
                int identifier = Unsafe.As<T, int>(ref field);
                var column = fields[searchField];
                var toSearch = queryData[searchField].values;
                var presence = queryData[searchField].desiredPresence;
                var task = column.Search(toSearch, presence, token, progress, identifier);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            tasks.Clear();
            results.Seal();
            SearchFinished?.Invoke(results);
            Console.WriteLine("Sealed results");
            return true;
        }

        private void OnPartialResultsSorted(KeyValuePair<int, K>[] sorted)
        {
            PartialResultsSorted?.Invoke(sorted);
        }
        public async Task<bool> CancelSearch()
        {
            try
            {
                cancellationTokenSource.Cancel();
                await Task.WhenAll(tasks);
                tasks.Clear();
                return true;
            }
            catch
            {
                // cancellation token source not yet defined
                // OR a list of tasks is null
                return false;
            }
        }

        public async Task<bool> Save()
        {
            try
            {
                var tasks = fields.Values.Select(x => x.AppendHot());
                foreach (var task in tasks)
                    task.Start();
                await Task.WhenAll(tasks);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

}