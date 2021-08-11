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
        public delegate void SearchFinishedHandler(SearchResults results);
        public delegate void PartialResultsHandler(KeyValuePair<int, SearchEntry>[] orderedBestResults);
        public event SearchFinishedHandler SearchFinished;
        public event PartialResultsHandler PartialResultsSorted;
        private LineDBCol<int> ratings;
        private LineDBCol<string> descriptions;
        private LineDBCol<string> titles;
        private LineDBCol<int[]> descriptionIndices;
        private LineDBCol<int[]> titleIndices;
        private LineDBCol<int[]> movieActors;
        private HashSet<int> available;
        private Dictionary<SearchCategory, LineDBCol> fields;
        private static Func<string, int> toInt = x => string.IsNullOrEmpty(x) ? -1 : int.Parse(x);
        private static Func<int, string> fromInt = x => x.ToString();
        private static Func<string, string> dummy = x => x;
        private static Func<string, int[]> toArrayInt = x => Utilities.ConvertEach(x.Split(","), toInt);
        private static Func<int[], string> fromArrayInt = x => string.Join(",", Utilities.ConvertEach(x, fromInt));
        private CancellationTokenSource cancellationTokenSource;
        private List<Task> tasks;
        private SearchResults results;
        public MovieDB()
        {
            ratings = new LineDBCol<int>("ratings", Encoding.ASCII, fromInt, toInt);
            descriptions = new LineDBCol<string>("text_descriptions", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            descriptionIndices = new LineDBCol<int[]>("descriptions", Encoding.ASCII, fromArrayInt, toArrayInt);
            titles = new LineDBCol<string>("text_titles", Encoding.UTF8, x => x.Replace("\n", ""), dummy);
            titleIndices = new LineDBCol<int[]>("titles", Encoding.ASCII, fromArrayInt, toArrayInt);
            movieActors = new LineDBCol<int[]>("actors", Encoding.ASCII, fromArrayInt, toArrayInt);
            fields = new Dictionary<SearchCategory, LineDBCol>()
            {
                //{SearchCategory.rating , ratings },
                //{SearchCategory.description , descriptions },
                {SearchCategory.descriptionIndices , descriptionIndices },
                {SearchCategory.titleIndices, titleIndices },
                {SearchCategory.actorsIndices, movieActors },
            };
            Task.WaitAll(ratings.Initialize(), descriptions.Initialize(),
                         descriptionIndices.Initialize(), titles.Initialize(),
                         titleIndices.Initialize(), movieActors.Initialize());
        }
        public void ProgressReporter(SearchBatch additions)
        {
            var category = (SearchCategory)additions.CategoryIdentifier;
            foreach (var (index, presence) in additions.BoolValues)
            {
                results.Add(index, category, presence);
            }
            Console.WriteLine(string.Format("{0} found {1} matches", category, additions.BoolValues.Count));
        }
        public void Search(Query query)
        {
            Task.Run(async () =>
            {
                if (tasks != null && tasks.Count > 0)
                    await CancelSearch();

                results = new SearchResults(5000, 100);
                results.ResultsOrdered += OnPartialResultsSorted;
                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;
                var progress = new Progress<SearchBatch>(ProgressReporter);
                tasks = new List<Task>();

                foreach (var searchField in query.QueryData.Keys.Intersect(fields.Keys))
                {
                    var column = fields[searchField];
                    var task = column.Search(query.QueryData[searchField], token, progress, (int)searchField);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                tasks.Clear();
                results.Seal();
                SearchFinished?.Invoke(results);
                Console.WriteLine("Sealed results");
            });
        }

        private void OnPartialResultsSorted(KeyValuePair<int, SearchEntry>[] sorted)
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