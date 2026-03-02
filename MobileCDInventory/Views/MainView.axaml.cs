using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MobileCDInventory.Views
{
    public partial class MainView : UserControl
    {
        // 1. Mobile Storage Path (Isolated App Data)
        private string _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "inventory.db");
        
        // 2. READ-ONLY Connection String
        private string _connectionString;
        
        private ObservableCollection<Album> _allAlbums = new ObservableCollection<Album>();

        public MainView()
        {
            InitializeComponent();
            
            _connectionString = $"Data Source={_dbPath};Version=3;Read Only=True;";
            
            // Link the UI List to our data collection
            var listAlbums = this.FindControl<ListBox>("ListAlbums");
            if (listAlbums != null) listAlbums.ItemsSource = _allAlbums;

            // Wire up the events manually
            var btnSync = this.FindControl<Button>("BtnSync");
            if (btnSync != null) btnSync.Click += BtnSync_Click;

            var txtSearch = this.FindControl<TextBox>("TxtSearch");
            if (txtSearch != null) txtSearch.TextChanged += TxtSearch_TextChanged;

            var cmbGenre = this.FindControl<ComboBox>("CmbGenre");
            if (cmbGenre != null) cmbGenre.SelectionChanged += CmbGenre_SelectionChanged;

            // If the database already exists on the phone, load it immediately!
            if (File.Exists(_dbPath))
            {
                LoadDataFromDB();
                LoadGenres(); // Ensure genres load on startup too
            }
        }

        private async void BtnSync_Click(object? sender, RoutedEventArgs e)
        {
            var lblStatus = this.FindControl<TextBlock>("LblStatus");
            var btnSync = this.FindControl<Button>("BtnSync");
            
            if (lblStatus == null || btnSync == null) return;

            // 1. Get the app window to open the native Android file picker
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                btnSync.IsEnabled = false;
                lblStatus.Text = "Waiting for file selection...";

                // 2. Open the File Picker
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select inventory.db (Check your OneDrive folder)",
                    AllowMultiple = false
                });

                // 3. If the user picked a file, copy it to our app's secure storage
                if (files.Count >= 1)
                {
                    lblStatus.Text = "Copying database...";
                    
                    // Open the file the user selected
                    await using var sourceStream = await files[0].OpenReadAsync();
                    
                    // Create/Overwrite the inventory.db in our local app space
                    using (var destinationStream = File.Create(_dbPath))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }
                    
                    lblStatus.Text = "Sync Complete!";
                    
                    // Reload the UI
                    LoadDataFromDB(); 
                    LoadGenres();
                }
                else
                {
                    lblStatus.Text = "Sync cancelled.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btnSync.IsEnabled = true;
            }
        }

        // Triggered when text is typed in the search box
        private void TxtSearch_TextChanged(object? sender, TextChangedEventArgs e)
        {
            LoadDataFromDB();
        }

        // Triggered when a new genre is picked from the dropdown
        private void CmbGenre_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            LoadDataFromDB();
        }

        private void LoadDataFromDB()
        {
            if (!File.Exists(_dbPath)) return;

            var txtSearch = this.FindControl<TextBox>("TxtSearch");
            var lblStatus = this.FindControl<TextBlock>("LblStatus");
            var cmbGenre = this.FindControl<ComboBox>("CmbGenre");
            
            if (txtSearch == null) return;

            try
            {
                _allAlbums.Clear(); 
                
                // Grab the current values from both UI controls
                string searchQ = txtSearch.Text ?? "";
                string selectedGenre = cmbGenre?.SelectedItem as string ?? "All Genres";

                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    
                    // Base query with 1=1 trick to easily stack filters
                    string sql = "SELECT * FROM albums WHERE 1=1";
                    
                    // Apply text filter
                    if (!string.IsNullOrEmpty(searchQ))
                    {
                        sql += " AND (Artist LIKE @q OR Title LIKE @q)";
                    }

                    // Apply genre filter
                    if (!string.IsNullOrEmpty(selectedGenre) && selectedGenre != "All Genres")
                    {
                        sql += " AND Genre = @genre";
                    }

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        // Safely inject parameters
                        if (!string.IsNullOrEmpty(searchQ))
                            cmd.Parameters.AddWithValue("@q", $"%{searchQ}%");

                        if (!string.IsNullOrEmpty(selectedGenre) && selectedGenre != "All Genres")
                            cmd.Parameters.AddWithValue("@genre", selectedGenre);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                _allAlbums.Add(new Album {
                                    UPC = r["UPC"].ToString() ?? "",
                                    Artist = r["Artist"].ToString() ?? "",
                                    Title = r["Title"].ToString() ?? "",
                                    Year = r["Year"].ToString() ?? "",
                                    Format = r["Format"].ToString() ?? "",
                                    Label = r["Label"].ToString() ?? "",
                                    Genre = r["Genre"]?.ToString() ?? "",
                                    Styles = r["Styles"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                
                if (lblStatus != null) lblStatus.Text = $"Showing {_allAlbums.Count} Albums";
            }
            catch (Exception ex)
            {
                if (lblStatus != null) lblStatus.Text = $"DB Error: {ex.Message}";
            }
        }
    
        private void LoadGenres()
        {
            if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath)) return;

            var cmbGenre = this.FindControl<ComboBox>("CmbGenre");
            if (cmbGenre == null) return;

            // Start with the default "All" option
            var genres = new System.Collections.Generic.List<string> { "All Genres" };

            using var conn = new System.Data.SQLite.SQLiteConnection(_connectionString);
            conn.Open();

            // Grab distinct genres from the 'albums' table
            string sql = "SELECT DISTINCT Genre FROM albums WHERE Genre IS NOT NULL AND Genre != '' ORDER BY Genre ASC";
            using var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                genres.Add(reader.GetString(0));
            }

            cmbGenre.ItemsSource = genres;
            cmbGenre.SelectedIndex = 0; // Default to "All Genres"
        }
    }

    // --- DATA MODEL ---
    public class Album 
    {
        public string UPC { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string Year { get; set; } = "";
        public string Format { get; set; } = "";
        public string Label { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Styles { get; set; } = "";
    }
}