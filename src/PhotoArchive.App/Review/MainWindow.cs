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

    private readonly TextBox inputPathTextBox = new();
    private readonly TextBox outputPathTextBox = new();
    private readonly TextBox databasePathTextBox = new();

    private readonly TextBox searchTextBox = new();
    private readonly TextBox yearTextBox = new();
    private readonly TextBox decadeTextBox = new();
    private readonly ComboBox statusComboBox = new();
    private readonly ComboBox tagFilterComboBox = new();
    private readonly ComboBox sortComboBox = new();
    private readonly CheckBox duplicatesOnlyCheckBox = new();
    private readonly CheckBox needsReviewCheckBox = new();
    private readonly ListBox photoListBox = new();
    private readonly TextBlock pageTextBlock = new();

    private readonly Image previewImage = new();
    private readonly TextBlock metadataTextBlock = new();
    private readonly TextBox dateTextBox = new();
    private readonly TextBox correctionReasonTextBox = new();
    private readonly TextBox tagNameTextBox = new();
    private readonly ComboBox tagTypeComboBox = new();
    private readonly ListBox currentTagsListBox = new();
    private readonly ListBox nearbyListBox = new();
    private readonly ListBox relatedListBox = new();
    private readonly ListBox duplicateListBox = new();
    private readonly ComboBox canonicalComboBox = new();

    private PhotoReviewRepository repository;
    private ReviewPhoto? selectedPhoto;
    private ReviewPhotoDetails? selectedDetails;
    private int pageNumber = 1;
    private const int PageSize = 24;

    public MainWindow(string databasePath)
    {
        repository = new PhotoReviewRepository(databasePath);
        databasePathTextBox.Text = databasePath;
        inputPathTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        outputPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoArchiveOutput");

        Title = "PhotoArchive";
        Width = 1280;
        Height = 820;
        MinWidth = 1040;
        MinHeight = 680;
        Content = BuildShell();
        ShowSetupView();
    }

    private Control BuildShell()
    {
        var root = new DockPanel();
        var nav = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(10)
        };

        var setupButton = new Button { Content = "Directory" };
        setupButton.Click += (_, _) => ShowSetupView();
        var homeButton = new Button { Content = "Home" };
        homeButton.Click += async (_, _) => await ShowHomeViewAsync();
        var editButton = new Button { Content = "Edit Image" };
        editButton.Click += async (_, _) => await ShowEditViewAsync(selectedPhoto);

        nav.Children.Add(setupButton);
        nav.Children.Add(homeButton);
        nav.Children.Add(editButton);
        nav.Children.Add(statusTextBlock);
        DockPanel.SetDock(nav, Dock.Top);
        root.Children.Add(nav);
        root.Children.Add(viewHost);
        return root;
    }

    private void ShowSetupView()
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Avalonia.Thickness(16),
            MaxWidth = 880
        };

        panel.Children.Add(new TextBlock { Text = "Directory Setup", FontSize = 22, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Choose the original folder, cleaned output folder, and local database. If the database is empty, preprocessing runs before opening the home view." });
        panel.Children.Add(LabeledControl("Original folder", inputPathTextBox));
        panel.Children.Add(LabeledControl("Cleaned output folder", outputPathTextBox));
        panel.Children.Add(LabeledControl("SQLite database", databasePathTextBox));

        var openButton = new Button { Content = "Open / Preprocess" };
        openButton.Click += async (_, _) => await OpenOrPreprocessAsync();
        panel.Children.Add(openButton);
        viewHost.Content = panel;
    }

    private async Task OpenOrPreprocessAsync()
    {
        try
        {
            SetStatus("Opening directory...");
            var result = await new DirectorySetupService().OpenOrPreprocessAsync(
                inputPathTextBox.Text ?? string.Empty,
                outputPathTextBox.Text ?? string.Empty,
                databasePathTextBox.Text ?? "photoarchive.db");
            repository = new PhotoReviewRepository(result.DatabasePath);
            SetStatus($"{result.Message} {result.FileCount} file(s).");
            await ShowHomeViewAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private async Task ShowHomeViewAsync()
    {
        await repository.InitializeAsync();
        ConfigureHomeControls();
        await LoadTagFilterAsync();
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(12)
        };

        panel.Children.Add(BuildHomeFilterBar());
        Grid.SetRow(panel.Children[^1], 0);
        panel.Children.Add(photoListBox);
        Grid.SetRow(photoListBox, 1);
        panel.Children.Add(BuildPaginationBar());
        Grid.SetRow(panel.Children[^1], 2);
        viewHost.Content = panel;
        await LoadPhotoPageAsync();
    }

    private void ConfigureHomeControls()
    {
        statusComboBox.ItemsSource = new object[] { "Any status" }.Concat(Enum.GetValues<ArchiveFileStatus>().Cast<object>()).ToArray();
        statusComboBox.SelectedIndex = statusComboBox.SelectedIndex < 0 ? 0 : statusComboBox.SelectedIndex;
        tagFilterComboBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(TagOption.DisplayName));
        sortComboBox.ItemsSource = Enum.GetValues<ReviewSortMode>();
        sortComboBox.SelectedItem ??= ReviewSortMode.DateAscending;
        photoListBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ReviewPhoto.DisplayTitle));
        photoListBox.SelectionChanged += (_, _) => selectedPhoto = photoListBox.SelectedItem as ReviewPhoto;
        photoListBox.DoubleTapped += async (_, _) => await ShowEditViewAsync(photoListBox.SelectedItem as ReviewPhoto);
    }

    private Control BuildHomeFilterBar()
    {
        var panel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,90,110,150,150,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };

        searchTextBox.PlaceholderText = "Search";
        yearTextBox.PlaceholderText = "Year";
        decadeTextBox.PlaceholderText = "Decade";
        duplicatesOnlyCheckBox.Content = "Duplicates";
        needsReviewCheckBox.Content = "Needs review";

        panel.Children.Add(searchTextBox);
        Grid.SetColumn(searchTextBox, 0);
        panel.Children.Add(yearTextBox);
        Grid.SetColumn(yearTextBox, 1);
        panel.Children.Add(decadeTextBox);
        Grid.SetColumn(decadeTextBox, 2);
        panel.Children.Add(tagFilterComboBox);
        Grid.SetColumn(tagFilterComboBox, 3);
        panel.Children.Add(statusComboBox);
        Grid.SetColumn(statusComboBox, 4);
        panel.Children.Add(sortComboBox);
        Grid.SetColumn(sortComboBox, 5);
        panel.Children.Add(duplicatesOnlyCheckBox);
        Grid.SetColumn(duplicatesOnlyCheckBox, 6);
        panel.Children.Add(needsReviewCheckBox);
        Grid.SetColumn(needsReviewCheckBox, 7);

        var applyButton = new Button { Content = "Apply" };
        applyButton.Click += async (_, _) =>
        {
            pageNumber = 1;
            await LoadPhotoPageAsync();
        };
        panel.Children.Add(applyButton);
        Grid.SetColumn(applyButton, 8);
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        return panel;
    }

    private Control BuildPaginationBar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        var previousButton = new Button { Content = "Previous" };
        previousButton.Click += async (_, _) =>
        {
            pageNumber = Math.Max(1, pageNumber - 1);
            await LoadPhotoPageAsync();
        };
        var nextButton = new Button { Content = "Next" };
        nextButton.Click += async (_, _) =>
        {
            pageNumber++;
            await LoadPhotoPageAsync();
        };
        var editButton = new Button { Content = "Edit Selected" };
        editButton.Click += async (_, _) => await ShowEditViewAsync(photoListBox.SelectedItem as ReviewPhoto);

        panel.Children.Add(previousButton);
        panel.Children.Add(nextButton);
        panel.Children.Add(editButton);
        panel.Children.Add(pageTextBlock);
        return panel;
    }

    private async Task LoadTagFilterAsync()
    {
        var current = tagFilterComboBox.SelectedItem as TagOption;
        var tags = await repository.GetTagOptionsAsync();
        tagFilterComboBox.ItemsSource = new object[] { "Any tag" }.Concat(tags.Cast<object>()).ToArray();
        tagFilterComboBox.SelectedItem = current is null ? tagFilterComboBox.Items[0] : tags.FirstOrDefault(tag => tag.Id == current.Id) ?? tagFilterComboBox.Items[0];
    }

    private async Task LoadPhotoPageAsync()
    {
        var page = await repository.GetPhotoPageAsync(BuildFilter(), pageNumber, PageSize);
        if (page.PageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await repository.GetPhotoPageAsync(BuildFilter(), pageNumber, PageSize);
        }

        photoListBox.ItemsSource = page.Photos;
        canonicalComboBox.ItemsSource = page.Photos;
        pageTextBlock.Text = $"Page {page.PageNumber} of {page.TotalPages} ({page.TotalCount} photos)";
        SetStatus(pageTextBlock.Text);
    }

    private ReviewFilter BuildFilter()
    {
        var status = statusComboBox.SelectedItem is ArchiveFileStatus selectedStatus ? selectedStatus : (ArchiveFileStatus?)null;
        var tagId = tagFilterComboBox.SelectedItem is TagOption selectedTag ? selectedTag.Id : (Guid?)null;
        var (from, to) = ResolveDateFilter();
        return new ReviewFilter(
            SearchText: searchTextBox.Text,
            Status: status,
            TagId: tagId,
            DuplicatesOnly: duplicatesOnlyCheckBox.IsChecked == true,
            UncertainOrUnprocessedOnly: needsReviewCheckBox.IsChecked == true,
            From: from,
            To: to,
            SortMode: sortComboBox.SelectedItem is ReviewSortMode sortMode ? sortMode : ReviewSortMode.DateAscending);
    }

    private async Task ShowEditViewAsync(ReviewPhoto? photo)
    {
        if (photo is null)
        {
            SetStatus("Select an image first.");
            return;
        }

        selectedPhoto = photo;
        selectedDetails = await repository.GetDetailsAsync(photo.Id);
        if (selectedDetails is null)
        {
            SetStatus("Image no longer exists in the database.");
            return;
        }

        ConfigureEditControls();
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,380"),
            ColumnSpacing = 12,
            Margin = new Avalonia.Thickness(12)
        };

        var previewPanel = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 10 };
        previewPanel.Children.Add(new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Avalonia.Thickness(1),
            Child = previewImage
        });
        Grid.SetRow(previewPanel.Children[^1], 0);
        previewPanel.Children.Add(metadataTextBlock);
        Grid.SetRow(metadataTextBlock, 1);

        grid.Children.Add(previewPanel);
        Grid.SetColumn(previewPanel, 0);
        grid.Children.Add(BuildEditPane());
        Grid.SetColumn(grid.Children[^1], 1);
        viewHost.Content = grid;
        await LoadSelectedDetailsAsync();
    }

    private void ConfigureEditControls()
    {
        tagTypeComboBox.ItemsSource = Enum.GetValues<TagType>();
        tagTypeComboBox.SelectedItem ??= TagType.Custom;
        currentTagsListBox.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
        nearbyListBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ReviewPhoto.DisplayTitle));
        relatedListBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(RelatedReviewPhoto.DisplayTitle));
        duplicateListBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ReviewPhoto.DisplayTitle));
        canonicalComboBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ReviewPhoto.DisplayTitle));
        previewImage.Stretch = Stretch.Uniform;
        previewImage.MinHeight = 300;
    }

    private Control BuildEditPane()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Metadata", FontSize = 20, FontWeight = FontWeight.SemiBold });
        dateTextBox.PlaceholderText = "yyyy-MM-dd HH:mm";
        correctionReasonTextBox.PlaceholderText = "Reason";
        panel.Children.Add(dateTextBox);
        panel.Children.Add(correctionReasonTextBox);
        var saveDateButton = new Button { Content = "Save Date" };
        saveDateButton.Click += async (_, _) => await CorrectDateAsync();
        panel.Children.Add(saveDateButton);

        panel.Children.Add(new TextBlock { Text = "Tags", FontWeight = FontWeight.SemiBold });
        panel.Children.Add(currentTagsListBox);
        tagNameTextBox.PlaceholderText = "Tag";
        panel.Children.Add(tagNameTextBox);
        panel.Children.Add(tagTypeComboBox);
        var tagButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var addTagButton = new Button { Content = "Add Tag" };
        addTagButton.Click += async (_, _) => await AddTagAsync();
        var removeTagButton = new Button { Content = "Remove Tag" };
        removeTagButton.Click += async (_, _) => await RemoveTagAsync();
        tagButtons.Children.Add(addTagButton);
        tagButtons.Children.Add(removeTagButton);
        panel.Children.Add(tagButtons);

        panel.Children.Add(new TextBlock { Text = "Nearby Dates", FontWeight = FontWeight.SemiBold });
        nearbyListBox.Height = 95;
        panel.Children.Add(nearbyListBox);
        panel.Children.Add(new TextBlock { Text = "Similar / Related", FontWeight = FontWeight.SemiBold });
        relatedListBox.Height = 120;
        panel.Children.Add(relatedListBox);
        panel.Children.Add(new TextBlock { Text = "Duplicates", FontWeight = FontWeight.SemiBold });
        duplicateListBox.Height = 80;
        panel.Children.Add(duplicateListBox);
        panel.Children.Add(canonicalComboBox);
        var duplicateButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var markDuplicateButton = new Button { Content = "Mark Duplicate" };
        markDuplicateButton.Click += async (_, _) => await MarkDuplicateAsync();
        var hideButton = new Button { Content = "Hide" };
        hideButton.Click += async (_, _) => await HideAsync();
        duplicateButtons.Children.Add(markDuplicateButton);
        duplicateButtons.Children.Add(hideButton);
        panel.Children.Add(duplicateButtons);
        return panel;
    }

    private async Task LoadSelectedDetailsAsync()
    {
        if (selectedPhoto is null)
        {
            return;
        }

        selectedDetails = await repository.GetDetailsAsync(selectedPhoto.Id);
        if (selectedDetails is null)
        {
            return;
        }

        var photo = selectedDetails.Photo;
        dateTextBox.Text = photo.InferredTakenDate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        currentTagsListBox.ItemsSource = selectedDetails.Tags;
        nearbyListBox.ItemsSource = selectedDetails.NearbyPhotos;
        relatedListBox.ItemsSource = selectedDetails.RelatedPhotos;
        duplicateListBox.ItemsSource = selectedDetails.DuplicateGroup;
        metadataTextBlock.Text = BuildMetadataText(selectedDetails);
        LoadPreview(photo);
    }

    private async Task CorrectDateAsync()
    {
        if (selectedPhoto is null)
        {
            return;
        }

        if (!DateTimeOffset.TryParse(dateTextBox.Text, out var date))
        {
            SetStatus("Invalid date.");
            return;
        }

        await repository.CorrectTakenDateAsync(selectedPhoto.Id, date, correctionReasonTextBox.Text ?? string.Empty);
        await LoadSelectedDetailsAsync();
        SetStatus("Date saved.");
    }

    private async Task AddTagAsync()
    {
        if (selectedPhoto is null || string.IsNullOrWhiteSpace(tagNameTextBox.Text))
        {
            return;
        }

        var tagType = tagTypeComboBox.SelectedItem is TagType selected ? selected : TagType.Custom;
        await repository.AddTagAsync(selectedPhoto.Id, tagNameTextBox.Text, tagType);
        tagNameTextBox.Text = string.Empty;
        await LoadTagFilterAsync();
        await LoadSelectedDetailsAsync();
        SetStatus("Tag added.");
    }

    private async Task RemoveTagAsync()
    {
        if (selectedPhoto is null || currentTagsListBox.SelectedItem is not Tag tag)
        {
            return;
        }

        await repository.RemoveTagAsync(selectedPhoto.Id, tag.Id);
        await LoadTagFilterAsync();
        await LoadSelectedDetailsAsync();
        SetStatus("Tag removed.");
    }

    private async Task MarkDuplicateAsync()
    {
        if (selectedPhoto is null || canonicalComboBox.SelectedItem is not ReviewPhoto canonical || canonical.Id == selectedPhoto.Id)
        {
            return;
        }

        await repository.MarkDuplicateAsync(selectedPhoto.Id, canonical.Id);
        await LoadSelectedDetailsAsync();
        SetStatus("Duplicate marked.");
    }

    private async Task HideAsync()
    {
        if (selectedPhoto is null)
        {
            return;
        }

        await repository.HideAsync(selectedPhoto.Id);
        await ShowHomeViewAsync();
        SetStatus("Hidden.");
    }

    private void LoadPreview(ReviewPhoto photo)
    {
        previewImage.Source = null;
        var path = photo.CurrentPath ?? photo.OriginalPath;
        if (!File.Exists(path) || photo.MediaKind is not MediaKind.SupportedImage)
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            previewImage.Source = new Bitmap(stream);
        }
        catch
        {
            previewImage.Source = null;
        }
    }

    private (DateTimeOffset? From, DateTimeOffset? To) ResolveDateFilter()
    {
        if (int.TryParse(yearTextBox.Text, out var year) && year is >= 1 and <= 9999)
        {
            var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return (from, from.AddYears(1).AddTicks(-1));
        }

        if (int.TryParse(decadeTextBox.Text, out var decadeStart) && decadeStart is >= 1 and <= 9990)
        {
            var normalizedStart = decadeStart / 10 * 10;
            var from = new DateTimeOffset(normalizedStart, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return (from, from.AddYears(10).AddTicks(-1));
        }

        return (null, null);
    }

    private static Control LabeledControl(string label, Control control)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                control
            }
        };
    }

    private static string BuildMetadataText(ReviewPhotoDetails details)
    {
        var metadata = details.Metadata;
        return string.Join(Environment.NewLine, new[]
        {
            details.Photo.OriginalFileName,
            details.Photo.CurrentPath ?? details.Photo.OriginalPath,
            $"Status: {details.Photo.Status}",
            $"Kind: {details.Photo.MediaKind}",
            $"Date: {details.Photo.DisplayDate}",
            $"Confidence: {details.Photo.DateConfidence}",
            $"Camera: {metadata?.CameraMake ?? string.Empty} {metadata?.CameraModel ?? string.Empty}".Trim(),
            $"Size: {metadata?.Width?.ToString() ?? "-"} x {metadata?.Height?.ToString() ?? "-"}",
            $"Hash: {details.Photo.Sha256Hash ?? "-"}"
        });
    }

    private void SetStatus(string message)
    {
        statusTextBlock.Text = message;
    }
}
