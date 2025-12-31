using System;
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

    private void TrackList_DoubleTapped(object? sender, TappedEventArgs e)
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
            CanResize = false
        };

        var textBox = new TextBox
        {
            Watermark = "Playlist name...",
            Margin = new Avalonia.Thickness(20)
        };

        var createButton = new Button
        {
            Content = "Create",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(20, 0, 20, 20)
        };

        createButton.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                viewModel.CreatePlaylistFromSelectedCommand.Execute(textBox.Text);
                dialog.Close();
            }
        };

        var panel = new StackPanel();
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

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
