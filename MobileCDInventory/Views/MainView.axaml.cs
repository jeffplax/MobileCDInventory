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

        private void CloseAbout_Click(object? sender, RoutedEventArgs e)
        {
            var mainTabs = this.FindControl<TabControl>("MainTabs");
            var aboutTab = this.FindControl<TabItem>("AboutTab");
            if(mainTabs != null)
            {
                mainTabs.SelectedIndex = 0; // Switch to the first tab (Master Library)
            }
            if(aboutTab != null)
            {
                aboutTab.IsVisible = false; // Hide the About tab
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
                    
                    await using var sourceStream = await files[0].OpenReadAsync();
                    using (var destinationStream = File.Create(wishlistDbPath))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }

                    if (DataContext is MainViewModel vm) 
                    {
                        vm.StatusMessage = "Wish List Imported!";
                        vm.LoadWishlistFromDB(); // Tell the ViewModel to reload the new file
                    }
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
    }
}