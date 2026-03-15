using System;
using System.IO;
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
    }
}