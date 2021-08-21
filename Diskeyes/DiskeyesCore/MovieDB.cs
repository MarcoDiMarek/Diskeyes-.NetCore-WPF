using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Table<SearchCategory, MovieSearchEntry> table;
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
            titles = new Column<string>("text_titles", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            titleIndices = new Column<int[]>("titles", Encoding.ASCII, fromArrayInt, toArrayInt);
            movieActors = new Column<int[]>("actors", Encoding.ASCII, fromArrayInt, toArrayInt);
            var columns = new Dictionary<SearchCategory, Column>()
            {
                //{SearchCategory.rating , ratings },
                //{SearchCategory.description , descriptions },
                {SearchCategory.descriptionIndices , descriptionIndices },
                {SearchCategory.titleIndices, titleIndices },
                {SearchCategory.actorsIndices, movieActors },
            };
            table = new Table<SearchCategory, MovieSearchEntry>(columns);
            table.PartialResultsSorted += OnResultsSorted;
            table.SearchFinished += OnSearchFinished;
        }
        public async Task<bool> Initialize()
        {
            await table.Initialize();
            ReadyStateChanged?.Invoke(true);
            return true;
        }

        private void OnResultsSorted(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            // A temporary function, needs to be replaced by a generic one which retrieves data values for collected indices
            //var progress = new Progress<SearchBatch<string>>(OnTitlesBatch);
            //var token = new CancellationTokenSource().Token;
            //Task.Run(() => titles.Retrieve(results.Select(x => x.Key).ToArray(), token, progress, (int)SearchCategory.title));
            
            PartialResultsSorted?.Invoke(results);
        }
        private void OnSearchFinished(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            SearchFinished?.Invoke(results);
        }

        public async Task<bool> Search(MovieQuery query)
        {
            return await table.Search(query);
        }

        public async Task<bool> CancelSearch()
        {
            return await table.CancelSearch();
        }

        public async Task<bool> Save()
        {
            return await table.Save();
        }
    }

}