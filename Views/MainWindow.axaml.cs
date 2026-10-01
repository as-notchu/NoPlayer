using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MusicPlayer.ViewModels;

namespace MusicPlayer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;

        // Keyboard transport: handled in the tunnel phase so the track list / buttons can't swallow the keys first
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);

        // Scrubbing: pause position updates from the player while the slider is being dragged
        ProgressSlider.AddHandler(PointerPressedEvent, ProgressSlider_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        ProgressSlider.AddHandler(PointerReleasedEvent, ProgressSlider_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        ProgressSlider.AddHandler(PointerCaptureLostEvent, ProgressSlider_PointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    // ---------------------------------------------------------------- keyboard

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel == null) return;

        // Never steal keys from text input
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.Space when e.KeyModifiers == KeyModifiers.None && focused is not CheckBox and not ToggleButton:
                viewModel.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left when ctrl:
                viewModel.PlayPreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right when ctrl:
                viewModel.PlayNextCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left when e.KeyModifiers == KeyModifiers.None:
                viewModel.SeekBackwardCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right when e.KeyModifiers == KeyModifiers.None:
                viewModel.SeekForwardCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ---------------------------------------------------------------- progress slider

    private void ProgressSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsSeeking = true;
        }
    }

    private void ProgressSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Let the slider finish updating its Value for this pointer event, then seek to it
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.SeekCommand.Execute(ProgressSlider.Value);
                viewModel.IsSeeking = false;
            }
        });
    }

    private void ProgressSlider_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsSeeking = false;
        }
    }

    // ---------------------------------------------------------------- library

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
            if (ViewModel is { } viewModel)
            {
                viewModel.UpdateMusicFolder(path);
                await viewModel.LoadLibraryCommand.ExecuteAsync(null);
            }
        }
    }

    private void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is Models.Track track && ViewModel is { } viewModel)
        {
            // The list also follows CurrentTrack (one-way binding), so skip the echo when playback moved on its own
            if (!ReferenceEquals(track, viewModel.CurrentTrack))
            {
                viewModel.PlayTrackCommand.Execute(track);
            }
        }
    }

    private async void AddDirectories_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;

        var textBox = DirectoriesTextBox;
        if (string.IsNullOrWhiteSpace(textBox.Text)) return;

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
    }

    // ---------------------------------------------------------------- playlists

    private void Playlist_Clicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Models.Playlist playlist && ViewModel is { } viewModel)
        {
            viewModel.SelectPlaylistCommand.Execute(playlist);
        }
    }

    private void DeletePlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Playlist playlist && ViewModel is { } viewModel)
        {
            e.Handled = true; // Prevent triggering playlist selection
            viewModel.DeletePlaylistCommand.Execute(playlist);
        }
    }

    private async void DeleteDirectoryPlaylist_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Playlist playlist && ViewModel is { } viewModel)
        {
            e.Handled = true; // Prevent triggering playlist selection
            viewModel.RemoveDirectoryPlaylistCommand.Execute(playlist);
            await viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }

    private void RemoveFromPlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.Track track && ViewModel is { } viewModel)
        {
            e.Handled = true; // Prevent triggering track selection
            viewModel.RemoveTrackFromPlaylistCommand.Execute(track);
        }
    }

    private async void CreatePlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;

        if (!viewModel.Tracks.Any(t => t.IsSelected))
        {
            viewModel.StatusMessage = "Check some songs first, then create a playlist";
            return;
        }

        var dialog = CreateDialog("New playlist", 420, 190);

        var textBox = new TextBox
        {
            Watermark = "Playlist name",
            Classes = { "input" }
        };

        var createButton = new Button
        {
            Content = "Create",
            Classes = { "primary" },
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "secondary" },
            IsCancel = true
        };

        createButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                viewModel.CreatePlaylistFromSelectedCommand.Execute(textBox.Text.Trim());
                dialog.Close();
            }
        };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = BuildDialogLayout(
            "Name your playlist",
            $"{viewModel.Tracks.Count(t => t.IsSelected)} checked song(s) will be added.",
            textBox,
            cancelButton,
            createButton);

        dialog.Opened += (_, _) => textBox.Focus();
        await dialog.ShowDialog(this);
    }

    private async void AddToPlaylist_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;

        if (!viewModel.Tracks.Any(t => t.IsSelected))
        {
            viewModel.StatusMessage = "Check some songs first, then add them to a playlist";
            return;
        }

        var customPlaylists = viewModel.Playlists.Where(p => !p.IsDirectoryPlaylist).ToList();

        if (customPlaylists.Count == 0)
        {
            viewModel.StatusMessage = "No custom playlists yet. Create one first!";
            return;
        }

        var dialog = CreateDialog("Add to playlist", 420, 360);

        var listBox = new ListBox
        {
            ItemsSource = customPlaylists,
            Classes = { "tracks" },
            MaxHeight = 200
        };

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.Playlist>((playlist, _) =>
            new TextBlock
            {
                Text = $"{playlist.Name}  ·  {playlist.Tracks.Count} tracks",
                Classes = { "track-title" },
                VerticalAlignment = VerticalAlignment.Center
            });

        var addButton = new Button
        {
            Content = "Add",
            Classes = { "primary" },
            IsDefault = true,
            IsEnabled = false
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "secondary" },
            IsCancel = true
        };

        listBox.SelectionChanged += (_, _) => addButton.IsEnabled = listBox.SelectedItem != null;
        listBox.DoubleTapped += (_, _) => addButton.Command?.Execute(null);

        addButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is Models.Playlist selectedPlaylist)
            {
                viewModel.AddSelectedToPlaylistCommand.Execute(selectedPlaylist);
                dialog.Close();
            }
        };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = BuildDialogLayout(
            "Choose a playlist",
            $"{viewModel.Tracks.Count(t => t.IsSelected)} checked song(s) will be added.",
            listBox,
            cancelButton,
            addButton);

        await dialog.ShowDialog(this);
    }

    // ---------------------------------------------------------------- dialog helpers

    private Window CreateDialog(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Background = this.FindResource("Bg0Brush") as IBrush ?? Brushes.Black
        };
    }

    private static Control BuildDialogLayout(string heading, string subtitle, Control body, Button cancel, Button confirm)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 17,
            FontWeight = FontWeight.Bold
        });
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Classes = { "secondary" },
            Margin = new Thickness(0, 4, 0, 14)
        });
        panel.Children.Add(body);
        panel.Children.Add(buttons);
        return panel;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
