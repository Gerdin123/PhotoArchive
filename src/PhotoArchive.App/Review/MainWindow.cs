using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed class MainWindow : Window
{
    private readonly ContentControl viewHost = new();
    private readonly TextBlock statusTextBlock = new();

    private PhotoReviewRepository repository;
    private string databasePath;
    private HomePage? homePage;
    private ReviewPhoto? selectedPhoto;

    public MainWindow(string databasePath)
    {
        this.databasePath = databasePath;
        repository = new PhotoReviewRepository(databasePath);

        Title = "PhotoArchive";
        Width = 1280;
        Height = 820;
        MinWidth = 720;
        MinHeight = 560;
        Background = UiTheme.AppBackground;
        Content = BuildShell();
        ShowSetupView();
    }

    private Control BuildShell()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = UiTheme.AppBackground
        };
        var nav = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"),
            ColumnSpacing = 8,
            Margin = new Avalonia.Thickness(12)
        };
        var statusBorder = new Border
        {
            Background = UiTheme.PanelBackground,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Child = nav
        };

        var setupButton = new Button { Content = "Directory" };
        setupButton.Click += (_, _) => ShowSetupView();
        var homeButton = new Button { Content = "Home" };
        homeButton.Click += async (_, _) => await ShowHomeViewAsync();
        var editButton = new Button { Content = "Edit Image" };
        editButton.Click += async (_, _) => await ShowEditViewAsync(selectedPhoto);
        statusTextBlock.VerticalAlignment = VerticalAlignment.Center;
        statusTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        statusTextBlock.Foreground = UiTheme.SecondaryText;

        nav.Children.Add(setupButton);
        Grid.SetColumn(setupButton, 0);
        nav.Children.Add(homeButton);
        Grid.SetColumn(homeButton, 1);
        nav.Children.Add(editButton);
        Grid.SetColumn(editButton, 2);
        nav.Children.Add(statusTextBlock);
        Grid.SetColumn(statusTextBlock, 3);
        DockPanel.SetDock(statusBorder, Dock.Top);
        root.Children.Add(statusBorder);
        root.Children.Add(viewHost);
        return root;
    }

    private void ShowSetupView()
    {
        viewHost.Content = new SetupPage(databasePath, OnDirectoryOpenedAsync, SetStatus);
    }

    private async Task OnDirectoryOpenedAsync(DirectorySetupResult result)
    {
        databasePath = result.DatabasePath;
        repository = new PhotoReviewRepository(result.DatabasePath);
        homePage = null;
        await ShowHomeViewAsync();
    }

    private async Task ShowHomeViewAsync()
    {
        if (homePage is null)
        {
            homePage = new HomePage(
                repository,
                SetStatus,
                photo => selectedPhoto = photo,
                ShowEditViewAsync);
            viewHost.Content = homePage;
            await homePage.InitializeAsync();
            return;
        }

        viewHost.Content = homePage;
        await homePage.RefreshAsync();
    }

    private async Task ShowEditViewAsync(ReviewPhoto? photo)
    {
        if (photo is null)
        {
            SetStatus("Select an image first.");
            return;
        }

        selectedPhoto = photo;
        var editPage = new EditPage(
            repository,
            photo,
            homePage?.CurrentPhotos ?? [],
            SetStatus,
            ShowHomeViewAsync);
        viewHost.Content = editPage;
        await editPage.InitializeAsync();
    }

    private void SetStatus(string message)
    {
        statusTextBlock.Text = message;
    }
}
