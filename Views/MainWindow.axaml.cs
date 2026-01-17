using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MusicPlayer.ViewModels;

namespace MusicPlayer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.UpdateMusicFolder(path);
                await viewModel.LoadLibraryCommand.ExecuteAsync(null);
            }
        }
    }

    private void ProgressSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Slider slider && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SeekCommand.Execute(slider.Value);
        }
    }

    private void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is Models.Track track && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PlayTrackCommand.Execute(track);
        }
    }

    private async void CreatePlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        // Simple input dialog
        var dialog = new Window
        {
            Title = "Create Playlist",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.Black
        };

        var textBox = new TextBox
        {
            Watermark = "Playlist name...",
            Margin = new Avalonia.Thickness(20),
            Classes = { "folder-input" }
        };

        var createButton = new Button
        {
            Content = "Create",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(20, 0, 20, 20),
            Classes = { "secondary-btn" }
        };

        createButton.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                viewModel.CreatePlaylistFromSelectedCommand.Execute(textBox.Text);
                dialog.Close();
            }
        };

        var panel = new StackPanel
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1a2e"))
        };
        panel.Children.Add(textBox);
        panel.Children.Add(createButton);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }

    private void Playlist_Clicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Models.Playlist playlist && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectPlaylistCommand.Execute(playlist);
        }
    }

    private void DeletePlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Playlist playlist && DataContext is MainWindowViewModel viewModel)
        {
            e.Handled = true; // Prevent triggering playlist selection
            viewModel.DeletePlaylistCommand.Execute(playlist);
        }
    }

    private async void DeleteDirectoryPlaylist_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Playlist playlist && DataContext is MainWindowViewModel viewModel)
        {
            e.Handled = true; // Prevent triggering playlist selection
            viewModel.RemoveDirectoryPlaylistCommand.Execute(playlist);
            await viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }

    private async void AddToPlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var customPlaylists = viewModel.Playlists.Where(p => !p.IsDirectoryPlaylist).ToList();

        if (customPlaylists.Count == 0)
        {
            viewModel.StatusMessage = "No custom playlists available. Create one first!";
            return;
        }

        // Create a simple playlist selector dialog
        var dialog = new Window
        {
            Title = "Add to Playlist",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brushes.Black
        };

        var listBox = new ListBox
        {
            ItemsSource = customPlaylists,
            Margin = new Avalonia.Thickness(20),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#16213e")),
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0f3460")),
            CornerRadius = new Avalonia.CornerRadius(8)
        };

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.Playlist>((playlist, _) =>
            new TextBlock
            {
                Text = $"{playlist.Name} ({playlist.Tracks.Count} tracks)",
                Padding = new Avalonia.Thickness(8),
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#e8e8e8"))
            });

        var addButton = new Button
        {
            Content = "Add",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(20, 0, 20, 20),
            Classes = { "secondary-btn" }
        };

        addButton.Click += (s, args) =>
        {
            if (listBox.SelectedItem is Models.Playlist selectedPlaylist)
            {
                viewModel.AddSelectedToPlaylistCommand.Execute(selectedPlaylist);
                dialog.Close();
            }
        };

        var panel = new StackPanel
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1a1a2e"))
        };
        panel.Children.Add(listBox);
        panel.Children.Add(addButton);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }

    private void RemoveFromPlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Track track && DataContext is MainWindowViewModel viewModel)
        {
            e.Handled = true; // Prevent triggering track selection
            viewModel.RemoveTrackFromPlaylistCommand.Execute(track);
        }
    }

    private async void AddDirectories_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var textBox = this.FindControl<TextBox>("DirectoriesTextBox");
        if (textBox == null || string.IsNullOrWhiteSpace(textBox.Text)) return;

        // Split by comma or newline
        var paths = textBox.Text
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paths.Count == 0)
        {
            viewModel.StatusMessage = "No valid directories entered";
            return;
        }

        // Add each directory
        foreach (var path in paths)
        {
            viewModel.AddMusicDirectory(path);
        }

        // Refresh library
        await viewModel.LoadLibraryCommand.ExecuteAsync(null);

        // Clear the text box
        textBox.Text = string.Empty;
        viewModel.StatusMessage = $"Added {paths.Count} directory(ies)";
    }

    private async void OpenDownloadWindow_Click(object? sender, RoutedEventArgs e)
    {
        var downloadWindow = new DownloadWindow();
        await downloadWindow.ShowDialog(this);

        // After download window closes, refresh the library if user wants
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Optionally reload library to include newly downloaded songs
            // Uncomment the line below if you want automatic refresh
            // await viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
