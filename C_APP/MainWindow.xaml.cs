using System;
using System.Data;
using System.IO;
using System.Net;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace C_APP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Timer AppTimer;                      // Timer for regular data update (15s)
        private DataTable DataTab = new DataTable(); // Result table

        private bool IsProcessing; // Indicator of data update being 'in process'
        private bool IsEnd;        // Indicator of finishing last page processing
        private string Result;     // String result from Server
        private string ConvertTo;  // Currenly selected Currency
        private string Path;       // Current path to catch correct server data
        private int Pages;         // Calculated number of pages (currencies/100 rounded up)
        private int PageCur;       // Curently processed page

        // Initialize Application

        public MainWindow()
        {
            InitializeComponent(); // Init Window
            InitTimer();           // Init Timer
            ConvertTo = "USD";     // Set initial currency
            UpdateData();          // Get the data
        }

        // Application Timer (triggers data update each 10 sec.)

        public void InitTimer()
        {
            AppTimer = new Timer();
            AppTimer.Elapsed += OnTimedEvent;
            AppTimer.Interval = 15000; // in miliseconds
            AppTimer.Start();
        }

        // Data update triggered by Timer

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (IsProcessing == false)
                Dispatcher.Invoke((Action)(() => UpdateData()));
        }

        // Refresh data from server (on click/time interval)

        private void RefreshData(object sender, RoutedEventArgs e)
        {
            if (IsProcessing == false)
                UpdateData();
        }

        // Get Data from Server

        public string GetResponse(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            request.Method = "GET";
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var content = string.Empty;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        content = sr.ReadToEnd();
                    }
                }
            }
            return content;
        }

        // Add one line to result table

        private void AddLine()
        {
            var start = Result.IndexOf("\"name\"", 0); 
            if (start != -1) // check for last record on last page
            {
                var values = new string[6];
                values[0] = GetValue("\"name\"", 9);

                // Process data according to position in input string
                if (ConvertTo == "USD" || ConvertTo == "CZK")
                {
                    values[3] = GetNumValue("\"total_supply\"", 15);
                    values[1] = GetNumValue("\"price\"", 9);
                    values[5] = GetNumValue("volume_24h", 13);
                    values[2] = GetNumValue("\"market_cap\"", 14);
                    values[4] = GetNumValue("change_24h", 13);
                }
                else // = EUR & BTC
                {
                    values[3] = GetNumValue("\"total_supply\"", 15);
                    values[1] = GetNumConv("\"price\"", 9);
                    values[5] = GetNumConv("volume_24h", 13);
                    values[2] = GetNumConv("\"market_cap\"", 14);
                    values[4] = GetNumConv("change_24h", 13);
                }

                // clear some null values
                if (values[2] == "null")
                    values[2] = "0";
                if (values[3] == "null")
                    values[3] = "0";
                if (values[5] == "null")
                    values[5] = "0";

                // check for duplicates
                try
                {
                    DataTab.Rows.Add(values);
                }
                catch 
                {
                    values[0] = values[0] + "_1"; // temp. solution, only 1 dupl. found
                    DataTab.Rows.Add(values);
                }
            }
            else
            {
                IsEnd = true; // trigger return
            }
        }

        // Create new result table with current server data

        private void UpdateData()
        {
            AppTimer.Stop();
            AppTimer.Start();

            IsProcessing = true; // locks update

            // Initialize new Data Table
            DataTab = new DataTable();
            DataTab.Columns.Add("Currency");
            DataTab.Columns.Add("Price (" + ConvertTo.ToString() + ")");
            DataTab.Columns.Add("Market Cap (" + ConvertTo.ToString() + ")");
            DataTab.Columns.Add("Total Tokens");
            DataTab.Columns.Add("Change in & (24h) (" + ConvertTo.ToString() + ")");
            DataTab.Columns.Add("Trade volume (24h) (" + ConvertTo.ToString() + ")");
            DataTab.PrimaryKey = new DataColumn[] { DataTab.Columns["Currency"] };

            // Fill table with values
            GetGlobalData();
            GetDataTable();
            DataGridVal.ItemsSource = new object[0];
            DataGridVal.ItemsSource =  DataTab.DefaultView;

            // Check selected Currency, if target value was reached
            WatchPrice();

            IsProcessing = false; // unlocks update
        }

        // Get current market global data
  
        private void GetGlobalData()
        {
            Path = "https://api.coinmarketcap.com/v2/global/?convert=" + ConvertTo;
            Result = GetResponse(Path);

            // Total number od currencies
            var value = GetNumValue("cryptocurrencies", 19);
            TotalCurrencies.Text = "Total Currencies: " + value.ToString();
            Pages = (int)Math.Ceiling(((float)Int32.Parse(value) / 100));

            // Total number od markets
            value = GetNumValue("markets", 10);
            TotalMarkets.Text = "Markets Active: " + value.ToString();

            // Total trade volume
            value = GetNumValue("total_market_cap", 19);
            TotalVolume.Text = "Total volume (" + ConvertTo.ToString() + "): " + value.ToString();

            // Bitcoin Market Share
            value = GetNumValue("of_market_cap", 16);
            BTCShare.Text = "BTC Market Share (%): " + value.ToString();
        }

        // Get concrete Cell value from 'Result' string (string)

        private string GetValue(string param, int shift)
        {
            var start = Result.IndexOf(param, 0) + shift;
            var end = Result.IndexOf(",", start);
            var value = Result.Substring(start, end - start - 1);
            Result = Result.Substring(start + 1);

            return value;
        }

        // Get concrete Cell value from 'Result' string (numeric value)

        private string GetNumValue(string param, int shift)
        {
            var start = Result.IndexOf(param, 0) + shift;
            var end = Result.IndexOf(",", start);
            var value = Result.Substring(start, end - start);

            return value;
        }

        // Get concrete Cell value from 'Result' string (numeric value, EUR & BTC)

        private string GetNumConv(string param, int shift)
        {
            var curr = Result.IndexOf(param, 0);
            var start = Result.IndexOf(param, curr + 1) + shift;
            var end = Result.IndexOf(",", start);
            var value = Result.Substring(start, end - start);

            return value;
        }

        // Get List with all currencies

        private void GetDataTable()
        {
            for (int i = 0; i < Pages; i++) // loop through calculated number of pages
            {
                // Create uri
                Path = "https://api.coinmarketcap.com/v2/ticker/?sort=id&start=" +
                        ((PageCur * 100) + 1).ToString() + "&limit=100&convert=" + ConvertTo;

                Result = GetResponse(Path);
                for (int x = 0; x < 100; x++) 
                {
                    AddLine(); // create record for each currency
                    if (IsEnd == true)
                    {
                        PageCur = 0;
                        IsEnd = false;
                        return;
                    }
                }
                PageCur += 1;
            }
            PageCur = 0; // reset pages
            IsEnd = false;
        }

        // Check for target price of selected Currency

        private void WatchPrice()
        {
            if (Watch_Value.IsChecked == true)
            {
                var line = DataTab.Rows.Find(Currency.Text);
                if (line != null)
                {
                    // Get current rounded price
                    try
                    {
                        var priceAll = line.ItemArray[1].ToString();
                        var priceRound = priceAll.Substring(0, priceAll.IndexOf("."));
                        var priceInt = int.Parse(priceRound);
                        var targetVal = int.Parse(TargetValue.Text);

                        // Check if target value has been reached (consider increase/decrease option)
                        bool raiseMsg = false;
                        if (OnIncrease.IsChecked == true)
                        {
                            if (priceInt > targetVal)
                                raiseMsg = true;
                        }
                        else
                        {
                            if (priceInt < targetVal)
                                raiseMsg = true;
                        }

                        // If conditions are met, raise warning window
                        if (raiseMsg == true)
                        {
                            MessageBoxResult result = MessageBox.Show(Currency + " has reached the watch value!",
                                "Confirmation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    } catch
                    {
                        // incorrect input
                    }
                }
            }
        }

        // Switch selected currency and trigger data update
   
        private void SwitchCurrency(object sender, RoutedEventArgs e)
        {
            var value = sender as RadioButton;
            ConvertTo = value.Content.ToString();
            if (IsProcessing == false)
                UpdateData();
        }
    }
}
