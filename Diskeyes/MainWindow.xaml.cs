using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DiskeyesCore;

namespace Diskeyes
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DiskeyesCore.MovieDB db;
        public MainWindow()
        {
            InitializeComponent();
            Task.Run(async () =>
            {
                db = new DiskeyesCore.MovieDB();
                await db.Initialize();
                db.SearchFinished += PrintResults;
                db.PartialResultsSorted += PrintPartialResults;
                Dispatcher.Invoke(() =>
                {
                    UIInfoText.Text = "Ready";
                    BusyBar.IsIndeterminate = false;
                });
            });
        }
        void PrintResults(KeyValuePair<int, MovieSearchEntry>[] orderedResults)
        {
            Dispatcher.Invoke(() =>
             {
                 UIInfoText.Text = "Here are top 10 matching lines sorted by score.\n";
                 int toTake = Math.Min(orderedResults.Length, 10);
                 foreach (var result in orderedResults.Take(toTake))
                 {
                     var entry = result.Value;
                     UIInfoText.Text += string.Format("Line index {0}, Score {1}, Actor matches [{2}] Title Matches [{3}] Description Matches [{4}]",
                         result.Key, result.Value.Score, string.Join(",", entry.actors.Select(x => x.ToString())), string.Join(",", entry.titleIndices.Select(x => x.ToString())), string.Join(",", entry.descriptionIndices.Select(x => x.ToString())));
                     UIInfoText.Text += "\n";
                 }
                 //UIInfoText.Text = string.Join(",", results.Results.Select(x => x.Key.ToString()));
                 BusyBar.IsIndeterminate = false;
             });
        }

        void PrintPartialResults(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            Dispatcher.Invoke(() =>
            {
                UIInfoText.Text = "Partial results return these lines: " + string.Join(",", results.Take(10).Select(x => x.Key.ToString()));
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string previousText = SearchBox.Text;
            UIInfoText.Text = previousText;

            if (previousText != string.Empty)
            {
                BusyBar.IsIndeterminate = true;
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    string newText = "";
                    Dispatcher.Invoke(() =>
                    {
                        newText = SearchBox.Text;
                    });
                    if (newText == previousText)
                    {
                        var query = new MovieQuery(newText);
                        Dispatcher.Invoke(() =>
                        {
                            if (query.QueryData.ContainsKey(SearchCategory.title))
                            {
                                UIInfoText.Text = "Searching titles " + string.Join(" ", query.QueryData[SearchCategory.title].values);
                            }
                            else if (query.QueryData.ContainsKey(SearchCategory.actors))
                            {
                                UIInfoText.Text = "Searching actors " + string.Join(" ", query.QueryData[SearchCategory.actors].values);
                            }
                        });
                        await db.Search(query);
                    }
                });
            }
        }
    }
}
