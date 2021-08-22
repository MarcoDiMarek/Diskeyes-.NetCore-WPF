using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiskeyesCore
{
    class MovieDB
    {
        public delegate void SearchFinishedHandler(KeyValuePair<int, MovieSearchEntry>[] results);
        public delegate void PartialResultsHandler(KeyValuePair<int, MovieSearchEntry>[] orderedBestResults);
        public delegate void ReadyStateHandler(bool ready);
        public event SearchFinishedHandler SearchFinished;
        public event PartialResultsHandler PartialResultsSorted;
        public event ReadyStateHandler ReadyStateChanged;
        private Table<SearchCategory, MovieSearchEntry> indexTable;
        private Table<SearchCategory, MovieSearchEntry> dataTable;
        private Column<int> ratings;
        private Column<string> descriptions;
        private Column<string> titles;
        private Column<int[]> descriptionIndices;
        private Column<int[]> titleIndices;
        private Column<int[]> movieActors;
        private static Func<string, int> toInt = x => string.IsNullOrEmpty(x) ? -1 : int.Parse(x);
        private static Func<int, string> fromInt = x => x.ToString();
        private static Func<string, string> dummy = x => x;
        private static Func<string, int[]> toArrayInt = x => Utilities.ConvertEach(x.Split(","), toInt);
        private static Func<int[], string> fromArrayInt = x => string.Join(",", Utilities.ConvertEach(x, fromInt));
        public MovieDB()
        {
            ratings = new Column<int>("ratings", Encoding.ASCII, fromInt, toInt);
            descriptions = new Column<string>("text_descriptions", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            descriptionIndices = new Column<int[]>("descriptions", Encoding.ASCII, fromArrayInt, toArrayInt);
            titles = new Column<string>("text_titles", Encoding.UTF8, x => x.Replace("\n", " "), dummy);
            titleIndices = new Column<int[]>("titles", Encoding.ASCII, fromArrayInt, toArrayInt);
            movieActors = new Column<int[]>("actors", Encoding.ASCII, fromArrayInt, toArrayInt);
            var searchColumns = new Dictionary<SearchCategory, Column>()
            {
                {SearchCategory.descriptionIndices , descriptionIndices },
                {SearchCategory.titleIndices, titleIndices },
                {SearchCategory.actorsIndices, movieActors },
            };
            var retrievalColumns = new Dictionary<SearchCategory, Column>()
            {
                {SearchCategory.title , titles },
            };
            indexTable = new Table<SearchCategory, MovieSearchEntry>(searchColumns);
            dataTable = new Table<SearchCategory, MovieSearchEntry>(retrievalColumns);
            indexTable.PartialResultsSorted += OnResultsSorted;
            indexTable.SearchFinished += OnSearchFinished;
        }
        public async Task<bool> Initialize()
        {
            await Task.WhenAll(indexTable.Initialize(), dataTable.Initialize());
            ReadyStateChanged?.Invoke(true);
            return true;
        }

        private void MergeRetrieved(SearchBatch<string> additions)
        {
            var category = (SearchCategory)additions.CategoryIdentifier;
            foreach (var (index, value) in additions.Values)
            {
                indexTable.results.Add(index, category, value);
            }
        }

        private void OnResultsSorted(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            PartialResultsSorted?.Invoke(results);
        }
        private void OnSearchFinished(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            Task.Run(async () =>
            {
                var indices = results.Take(10).Where(x => x.Value.title == null).Select(x => x.Key).ToHashSet();
                var progress = new Progress<SearchBatch<string>>(MergeRetrieved);
                var token = new CancellationTokenSource().Token;
                var retrievalTask = titles.Retrieve(indices, token, progress, (int)SearchCategory.title);
                await retrievalTask;
                var updated = new Dictionary<int, MovieSearchEntry>(results.Take(10));
                foreach (var (index, value) in retrievalTask.Result)
                    updated[index].title = value;


                SearchFinished?.Invoke(updated.OrderByDescending(x => x.Value.Score).ToArray());
            });
        }

        public async Task<bool> Search(MovieQuery query)
        {
            return await indexTable.Search(query);
        }

        public async Task<bool> CancelSearch()
        {
            return await indexTable.CancelSearch();
        }

        public async Task<bool> Save()
        {
            return await indexTable.Save();
        }
    }

}