using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic; // NEW: Required for the List<WishlistItem>
using System.Linq; 
using Microsoft.Data.Sqlite;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MobileCDInventory.ViewModels;

namespace MobileCDInventory.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            // Manually ensure the DataContext is set if it isn't already
            if (DataContext == null)
            {
                DataContext = new MainViewModel();
            }
            
            // NEW: Load the wishlist as soon as the view initializes
            LoadWishlistFromDB();
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            
            var lblVersion = this.FindControl<TextBlock>("LblVersion");
            var lblBuildDate = this.FindControl<TextBlock>("LblBuildDate");

            if (lblVersion != null && version != null)
            {
                lblVersion.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }

            // Read the exact date string we injected via the .csproj file
            var buildDateAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                                        .Cast<System.Reflection.AssemblyMetadataAttribute>()
                                        .FirstOrDefault(attr => attr.Key == "BuildDate");

            if (lblBuildDate != null && buildDateAttr != null)
            {
                lblBuildDate.Text = $"Build Date: {buildDateAttr.Value}";
            }

        }

        private async void BtnSync_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select inventory.db (Check your OneDrive folder)",
                    AllowMultiple = false
                });

                if (files.Count >= 1)
                {
                    // Use a single 'if' check to get our ViewModel
                    if (DataContext is MainViewModel vm)
                    {
                        // 1. Close connection so we can overwrite the file
                        vm.CloseConnection();

                        // 2. Define the path
                        var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var dbPath = Path.Combine(vaultFolder, "inventory.db");   
                        
                        // 3. Copy the file
                        await using var sourceStream = await files[0].OpenReadAsync();
                        using (var destinationStream = File.Create(dbPath))
                        {
                            await sourceStream.CopyToAsync(destinationStream);
                        }

                        // 4. Update the UI and Connect
                        vm.StatusMessage = "File picked. Connecting...";
                        vm.ConnectToDatabase(dbPath);
                    }
                    else
                    {
                        Console.WriteLine("DEBUG: DataContext is null or wrong type!");
                    }
                }
            }
            catch (Exception ex)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusMessage = $"Sync Error: {ex.Message}";
                }
            }
        }

        // ====================================================================
        // Wish List Sidecar Database Logic
        // ====================================================================
        private void BtnSaveWish_Click(object? sender, RoutedEventArgs e)
        {
            // 1. Validate input
            if (string.IsNullOrWhiteSpace(TxtWishTitle.Text))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusMessage = "Wishlist: Title is required.";
                }
                return; 
            }

            try
            {
                // 2. Route the sidecar database to the secure mobile app data folder
                var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");
                string connectionString = $"Data Source={wishlistDbPath};";

                // 3. Open connection and execute insert
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    
                    // Create the table on the fly if this is the first time adding an item
                    string createTableSql = @"CREATE TABLE IF NOT EXISTS ""wishlist"" (
                                                ""WishID"" INTEGER PRIMARY KEY AUTOINCREMENT,
                                                ""Artist"" TEXT,
                                                ""Title"" TEXT NOT NULL,
                                                ""Format"" TEXT,
                                                ""Notes"" TEXT,
                                                ""DateAdded"" TEXT
                                            );";
                    using (var createCmd = new SqliteCommand(createTableSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    // Insert the new target
                    string insertSql = @"INSERT INTO wishlist (Artist, Title, Format, Notes, DateAdded) 
                                         VALUES (@artist, @title, @format, @notes, @dateAdded)";
                                         
                    using (var cmd = new SqliteCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@artist", TxtWishArtist.Text ?? "");
                        cmd.Parameters.AddWithValue("@title", TxtWishTitle.Text);
                        
                        // Extract the format from the ComboBox
                        string formatVal = "";
                        if (CmbWishFormat.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
                        {
                            formatVal = cbi.Content.ToString() ?? "";
                        }
                        cmd.Parameters.AddWithValue("@format", formatVal);
                        
                        cmd.Parameters.AddWithValue("@notes", TxtWishNotes.Text ?? "");
                        cmd.Parameters.AddWithValue("@dateAdded", DateTime.Now.ToString("yyyy-MM-dd"));
                        
                        cmd.ExecuteNonQuery();
                    }
                }

                // 4. Clear the UI fields after a successful save
                TxtWishArtist.Text = "";
                TxtWishTitle.Text = "";
                TxtWishNotes.Text = "";
                CmbWishFormat.SelectedIndex = 0;

                // 5. Update Status
                if (DataContext is MainViewModel vmSuccess)
                {
                    vmSuccess.StatusMessage = "Saved to Wish List!";
                }

                // NEW: Instantly refresh the grid to show the new item
                LoadWishlistFromDB();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save wishlist item: {ex.Message}");
                if (DataContext is MainViewModel vmError)
                {
                    vmError.StatusMessage = "Error saving to Wish List.";
                }
            }
        }

        // ====================================================================
        // Wish List Import / Export Logic
        // ====================================================================
        private async void BtnImportWish_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select wishlist.db (Check your OneDrive folder)",
                    AllowMultiple = false
                });

                if (files.Count >= 1)
                {
                    var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");   
                    
                    // Copy from OneDrive to Android Vault
                    await using var sourceStream = await files[0].OpenReadAsync();
                    using (var destinationStream = File.Create(wishlistDbPath))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }

                    if (DataContext is MainViewModel vm) vm.StatusMessage = "Wish List Imported!";
                    
                    // Refresh the grid instantly
                    LoadWishlistFromDB(); 
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Import Error: {ex.Message}");
                if (DataContext is MainViewModel vm) vm.StatusMessage = "Error importing Wish List.";
            }
        }

        private async void BtnExportWish_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");   

                if (!File.Exists(wishlistDbPath))
                {
                    if (DataContext is MainViewModel vmErr) vmErr.StatusMessage = "No Wish List to export yet!";
                    return;
                }

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save wishlist.db to OneDrive",
                    SuggestedFileName = "wishlist.db"
                });

                if (file != null)
                {
                    // Copy from Android Vault up to OneDrive
                    using var sourceStream = File.OpenRead(wishlistDbPath);
                    await using var destinationStream = await file.OpenWriteAsync();
                    await sourceStream.CopyToAsync(destinationStream);
                    
                    if (DataContext is MainViewModel vm) vm.StatusMessage = "Wish List Exported!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Export Error: {ex.Message}");
                if (DataContext is MainViewModel vm) vm.StatusMessage = "Error exporting Wish List.";
            }
        }

        // ====================================================================
        // Load Wishlist Logic
        // ====================================================================
        private void LoadWishlistFromDB()
        {
            var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");
            
            // If the database doesn't exist yet, there's nothing to load
            if (!File.Exists(wishlistDbPath)) return;

            string connectionString = $"Data Source={wishlistDbPath};";
            var items = new List<WishlistItem>();

            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    // Pull the items, newest first
                    string sql = "SELECT WishID, Artist, Title, Format, Notes FROM wishlist ORDER BY DateAdded DESC";
                    
                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new WishlistItem
                            {
                                wishID = Convert.ToInt32(reader["WishID"]),
                                Artist = reader["Artist"]?.ToString() ?? "",
                                Title = reader["Title"]?.ToString() ?? "",
                                Format = reader["Format"]?.ToString() ?? "",
                                Notes = reader["Notes"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                // Explicitly find the grid and bind the data (Bypasses compilation issues)
                var gridWishlist = this.FindControl<DataGrid>("GridWishlist");
                if (gridWishlist != null)
                {
                    gridWishlist.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load wishlist: {ex.Message}");
            }
        }

        private void BtnDeleteWish_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WishlistItem itemToDelete)
            {
                try
                {
                    var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");   
                    string connectionString = $"Data Source={wishlistDbPath};";

                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        string deleteSql = "DELETE FROM wishlist WHERE WishID = @wishID";
                        using (var cmd = new SqliteCommand(deleteSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@wishID", itemToDelete.wishID);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Refresh the grid after deletion
                    LoadWishlistFromDB();

                    if (DataContext is MainViewModel vm) 
                    {
                        vm.StatusMessage = "Wish List item deleted.";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete wishlist item: {ex.Message}");
                    if (DataContext is MainViewModel vm) 
                    {
                        vm.StatusMessage = "Error deleting Wish List item.";
                    }
                }
            }
        }
    }

    

    // ====================================================================
    // Wish List Data Model
    // ====================================================================
    public class WishlistItem
    {
        public int wishID { get; set; } // required for internal handling, but not displayed in the grid
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string Format { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}