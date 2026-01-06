using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MusicPlayer.ViewModels;

namespace MusicPlayer.Views;

public partial class DownloadWindow : Window
{
    public DownloadWindow()
    {
        InitializeComponent();
        DataContext = new DownloadWindowViewModel();
    }

    private async void BrowseOutput_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as DownloadWindowViewModel;
        if (viewModel == null) return;

        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Directory",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            viewModel.OutputDirectory = folder[0].Path.LocalPath;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Dispose the view model when window closes
        if (DataContext is DownloadWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }
}
