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
        public delegate void SearchFinishedHandler(SearchResults<SearchCategory, MovieSearchEntry> results);
        public delegate void PartialResultsHandler(KeyValuePair<int, MovieSearchEntry>[] orderedBestResults);
        public delegate void ReadyStateHandler(bool ready);
        public event SearchFinishedHandler SearchFinished;
        public event PartialResultsHandler PartialResultsSorted;
        public event ReadyStateHandler ReadyStateChanged;
        private Table<SearchCategory, MovieSearchEntry> table;
        private LineDBCol<int> ratings;
        private LineDBCol<string> descriptions;
        private LineDBCol<string> titles;
        private LineDBCol<int[]> descriptionIndices;
        private LineDBCol<int[]> titleIndices;
        private LineDBCol<int[]> movieActors;
        private static Func<string, int> toInt = x => string.IsNullOrEmpty(x) ? -1 : int.Parse(x);
        private static Func<int, string> fromInt = x => x.ToString();
        private static Func<string, string> dummy = x => x;
        private static Func<string, int[]> toArrayInt = x => Utilities.ConvertEach(x.Split(","), toInt);
        private static Func<int[], string> fromArrayInt = x => string.Join(",", Utilities.ConvertEach(x, fromInt));
        public MovieDB()
        {
            ratings = new LineDBCol<int>("ratings", Encoding.ASCII, fromInt, toInt);
            descriptions = new LineDBCol<string>("text_descriptions", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            descriptionIndices = new LineDBCol<int[]>("descriptions", Encoding.ASCII, fromArrayInt, toArrayInt);
            titles = new LineDBCol<string>("text_titles", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            titleIndices = new LineDBCol<int[]>("titles", Encoding.ASCII, fromArrayInt, toArrayInt);
            movieActors = new LineDBCol<int[]>("actors", Encoding.ASCII, fromArrayInt, toArrayInt);
            var columns = new Dictionary<SearchCategory, LineDBCol>()
            {
                //{SearchCategory.rating , ratings },
                //{SearchCategory.description , descriptions },
                {SearchCategory.descriptionIndices , descriptionIndices },
                {SearchCategory.titleIndices, titleIndices },
                {SearchCategory.actorsIndices, movieActors },
            };
            table = new Table<SearchCategory, MovieSearchEntry>(columns);
            table.PartialResultsSorted += OnResultsSorted;
            table.SearchFinished += OnSearchDone;
        }
        public async Task<bool> Initialize()
        {
            await table.Initialize();
            ReadyStateChanged?.Invoke(true);
            return true;
        }
        private void OnResultsSorted(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            PartialResultsSorted?.Invoke(results);
        }
        private void OnSearchDone(SearchResults<SearchCategory, MovieSearchEntry> results)
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