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
                    SearchBox.IsEnabled = true;
                });
            });
        }
        void PrintResults(KeyValuePair<int, MovieSearchEntry>[] orderedResults)
        {
            Dispatcher.Invoke(() =>
             {
                 if (orderedResults.Length == 0)
                 {
                     UIInfoText.Text = "No matches.";
                 }
                 else
                 {
                     UIInfoText.Text = "Here are top 10 matching lines sorted by score.\n";
                     int toTake = Math.Min(orderedResults.Length, 10);
                     foreach (var result in orderedResults.Take(toTake))
                     {
                         var entry = result.Value;
                         UIInfoText.Text += string.Format("File line index {0}, Score {1}, Actor matches [{2}] Title Matches [{3}] Description Matches [{4}]",
                             result.Key + 2, result.Value.Score, string.Join(",", entry.actors.Select(x => x.ToString())), string.Join(",", entry.titleIndices.Select(x => x.ToString())), string.Join(",", entry.descriptionIndices.Select(x => x.ToString())));
                         UIInfoText.Text += "\n";
                     }
                 }
                 BusyBar.IsIndeterminate = false;
             });
        }

        void PrintPartialResults(KeyValuePair<int, MovieSearchEntry>[] results)
        {
            Dispatcher.Invoke(() =>
            {
                UIInfoText.Text = "PARTIAL TOP 10 RESULTS FROM 2000 MATCHES \n\n";
                int toTake = Math.Min(results.Length, 10);
                foreach (var result in results.Take(toTake))
                {
                    var entry = result.Value;
                    UIInfoText.Text += string.Format("File line index {0}, Score {1}, Actor matches [{2}] Title Matches [{3}] Description Matches [{4}]",
                        result.Key + 2, result.Value.Score, string.Join(",", entry.actors.Select(x => x.ToString())), string.Join(",", entry.titleIndices.Select(x => x.ToString())), string.Join(",", entry.descriptionIndices.Select(x => x.ToString())));
                    UIInfoText.Text += "\n";
                }
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var previousText = SearchBox.Text;
            if (SearchBox.Text != string.Empty)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(100);
                    Dispatcher.Invoke(() =>
                    {
                        previousText = SearchBox.Text;
                    });
                    await db.CancelSearch();
                    await Task.Delay(200);
                    string newText = "";
                    Dispatcher.Invoke(() =>
                    {
                        newText = SearchBox.Text;
                    });

                    if (previousText != newText)
                        return;

                    var query = new MovieQuery(newText);

                    Dispatcher.Invoke(() =>
                    {
                        BusyBar.IsIndeterminate = true;
                        UIInfoText.Text = "Searching \n";
                        foreach (var searchCategory in query.QueryData)
                        {
                            UIInfoText.Text += string.Format("{0} : [{1}]\n",
                                               searchCategory.Key.ToString(),
                                               string.Join(",", searchCategory.Value.values));
                        }
                    });

                    await db.Search(query);
                });
            }
            else
            {
                Task.Run(async () =>
                {
                    await db.CancelSearch();
                    Dispatcher.Invoke(() => { UIInfoText.Text = "Cancelled"; });
                });

            }
        }

        private void UITutorial_Loaded(object sender, RoutedEventArgs e)
        {
            var messages = new List<(string, int)>()
            {
                ("Titles are searched without parantheses and commas.", 4),
                ("Try Lara Croft", 2),
                ("Try actors(NOT Angelina Jolie) Tomb Raider", 4),
                ("Try pirates actors(Johny Depp)", 4),
                ("Try Amélie actors(Audrey Tautou)", 4),
                ("Try Tomb Raider actors(Angelina Jolie)", 4),
                ("Try hakunamatata", 4),
                ("Feel free to update the query at any time", 4),
            };

            Task.Run(async () =>
            {
                await Task.Delay(2000);
                while (true)
                {
                    foreach (var (message, duration) in messages)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UITutorial.Text = message;
                        });
                        await Task.Delay(duration * 1000);
                    }
                }
            });

        }
    }
}
