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
        MovieDB db;
        public MainWindow()
        {
            InitializeComponent();
            Task.Run(() =>
            {
                db = new MovieDB();
                db.SearchFinished += PrintResults;
                db.PartialResultsSorted += PrintPartialResults;
                Dispatcher.Invoke(() =>
                {
                    SomeTextBox.Text = "Ready";
                });
            });
        }
        void PrintResults(SearchResults results)
        {
            Dispatcher.Invoke(() =>
             {
                 SomeTextBox.Text = string.Join(",", results.Results.Select(x => x.Value.index.ToString()));
             });
        }

        void PrintPartialResults(KeyValuePair<int, SearchEntry>[] results)
        {
            Dispatcher.Invoke(() =>
            {
                SomeTextBox.Text = "Partial " + string.Join(",", results.Take(10).Select(x => x.Value.index.ToString()));
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string previousText = SearchBox.Text;
            if (previousText != string.Empty)
            {
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
                        var query = new Query(newText);
                        Dispatcher.Invoke(() =>
                        {
                            if (query.QueryData.ContainsKey(SearchCategory.titleIndices))
                            {
                                SomeTextBox.Text = "Searching titles " + string.Join(" ", query.QueryData[SearchCategory.titleIndices]);
                            }
                            else if (query.QueryData.ContainsKey(SearchCategory.actorsIndices))
                            {
                                SomeTextBox.Text = "Searching actors " + string.Join(" ", query.QueryData[SearchCategory.actorsIndices]);
                            }
                        });
                        db.Search(query);
                    }
                });
            }
        }
    }
}
