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

            // If the database already exists on the phone, load it immediately!
            if (File.Exists(_dbPath))
            {
                LoadDataFromDB();
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
        private void TxtSearch_TextChanged(object? sender, TextChangedEventArgs e)
        {
            LoadDataFromDB();
        }

        private void LoadDataFromDB()
        {
            if (!File.Exists(_dbPath)) return;

            var txtSearch = this.FindControl<TextBox>("TxtSearch");
            var lblStatus = this.FindControl<TextBlock>("LblStatus");
            
            if (txtSearch == null) return;

            try
            {
                _allAlbums.Clear(); 
                string searchQ = txtSearch.Text ?? "";

                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM albums";
                    
                    if (!string.IsNullOrEmpty(searchQ))
                    {
                        sql += " WHERE Artist LIKE @q OR Title LIKE @q OR Genre LIKE @q";
                    }

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        if (!string.IsNullOrEmpty(searchQ))
                            cmd.Parameters.AddWithValue("@q", $"%{searchQ}%");

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