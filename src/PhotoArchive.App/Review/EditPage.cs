using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed class EditPage : UserControl
{
    private readonly PhotoReviewRepository repository;
    private readonly ReviewPhoto photo;
    private readonly IReadOnlyList<ReviewPhoto> canonicalOptions;
    private readonly Action<string> setStatus;
    private readonly Func<Task> hiddenAsync;

    private readonly Image previewImage = new();
    private readonly ContentControl previewContent = new();
    private readonly TextBlock metadataTextBlock = new();
    private readonly TextBox titleTextBox = new();
    private readonly TextBox dateTextBox = new();
    private readonly TextBox tagNameTextBox = new();
    private readonly ComboBox tagTypeComboBox = new();
    private readonly ListBox currentTagsListBox = new();
    private readonly ListBox nearbyListBox = new();
    private readonly ListBox relatedListBox = new();
    private readonly ListBox duplicateListBox = new();
    private readonly ComboBox canonicalComboBox = new();

    private ReviewPhotoDetails? details;

    public EditPage(
        PhotoReviewRepository repository,
        ReviewPhoto photo,
        IReadOnlyList<ReviewPhoto> canonicalOptions,
        Action<string> setStatus,
        Func<Task> hiddenAsync)
    {
        this.repository = repository;
        this.photo = photo;
        this.canonicalOptions = canonicalOptions;
        this.setStatus = setStatus;
        this.hiddenAsync = hiddenAsync;

        ConfigureControls();
        Content = Build();
    }

    public async Task InitializeAsync()
    {
        details = await repository.GetDetailsAsync(photo.Id);
        if (details is null)
        {
            setStatus("Image no longer exists in the database.");
            return;
        }

        await LoadDetailsAsync();
    }

    private Control Build()
    {
        var grid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 16,
            Margin = new Avalonia.Thickness(16)
        };

        var previewPanel = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 10 };
        previewPanel.Children.Add(new Border
        {
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            Background = UiTheme.PanelBackground,
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(10),
            Child = previewContent
        });
        Grid.SetRow(previewPanel.Children[^1], 0);
        previewPanel.Children.Add(metadataTextBlock);
        Grid.SetRow(metadataTextBlock, 1);

        grid.Children.Add(previewPanel);
        Grid.SetColumn(previewPanel, 0);
        var splitter = new GridSplitter
        {
            Width = 6,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = UiTheme.Border
        };
        var editPane = BuildEditPane();
        editPane.MinWidth = 280;
        editPane.Width = 420;
        grid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);
        grid.Children.Add(editPane);
        ApplyResponsiveLayout(grid, previewPanel, splitter, editPane, wide: true);
        SizeChanged += (_, _) => ApplyResponsiveLayout(grid, previewPanel, splitter, editPane, Bounds.Width >= 900);
        return grid;
    }

    private static void ApplyResponsiveLayout(Grid grid, Control previewPanel, Control splitter, Control editPane, bool wide)
    {
        if (wide)
        {
            grid.ColumnDefinitions = new ColumnDefinitions("*,6,420");
            grid.RowDefinitions = new RowDefinitions("*");
            splitter.IsVisible = true;
            editPane.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            Grid.SetColumn(previewPanel, 0);
            Grid.SetRow(previewPanel, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetRow(splitter, 0);
            Grid.SetColumn(editPane, 2);
            Grid.SetRow(editPane, 0);
            return;
        }

        grid.ColumnDefinitions = new ColumnDefinitions("*");
        grid.RowDefinitions = new RowDefinitions("Auto,Auto");
        splitter.IsVisible = false;
        editPane.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Grid.SetColumn(previewPanel, 0);
        Grid.SetRow(previewPanel, 0);
        Grid.SetColumn(editPane, 0);
        Grid.SetRow(editPane, 1);
    }

    private void ConfigureControls()
    {
        tagTypeComboBox.ItemsSource = Enum.GetValues<TagType>();
        tagTypeComboBox.SelectedItem = TagType.Custom;
        currentTagsListBox.ItemTemplate = new FuncDataTemplate<Tag>((tag, _) =>
            tag is null ? new TextBlock() : BuildTagListItem(tag));
        nearbyListBox.ItemTemplate = new FuncDataTemplate<ReviewPhoto>((photo, _) =>
            photo is null ? new TextBlock() : PhotoPreviewControls.BuildPhotoListItem(photo));
        relatedListBox.ItemTemplate = new FuncDataTemplate<RelatedReviewPhoto>((related, _) =>
            related is null ? new TextBlock() : PhotoPreviewControls.BuildRelatedPhotoListItem(related));
        duplicateListBox.ItemTemplate = new FuncDataTemplate<ReviewPhoto>((photo, _) =>
            photo is null ? new TextBlock() : PhotoPreviewControls.BuildPhotoListItem(photo));
        canonicalComboBox.ItemTemplate = new FuncDataTemplate<ReviewPhoto>((option, _) =>
            option is null ? new TextBlock() : PhotoPreviewControls.BuildPhotoListItem(option));
        canonicalComboBox.ItemsSource = canonicalOptions;
        previewImage.Stretch = Stretch.Uniform;
        previewImage.MinHeight = 300;
        previewContent.MinHeight = 300;
    }

    private Control BuildEditPane()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Metadata", FontSize = 20, FontWeight = FontWeight.SemiBold });
        titleTextBox.PlaceholderText = "Title";
        panel.Children.Add(titleTextBox);
        var saveTitleButton = new Button { Content = "Save Title" };
        saveTitleButton.Click += async (_, _) => await SaveTitleAsync();
        panel.Children.Add(saveTitleButton);
        dateTextBox.PlaceholderText = "yyyy-MM-dd HH:mm";
        panel.Children.Add(dateTextBox);
        var saveDateButton = new Button { Content = "Save Date" };
        saveDateButton.Click += async (_, _) => await CorrectDateAsync();
        panel.Children.Add(saveDateButton);

        panel.Children.Add(new TextBlock { Text = "Tags", FontWeight = FontWeight.SemiBold });
        panel.Children.Add(currentTagsListBox);
        tagNameTextBox.PlaceholderText = "Tag";
        panel.Children.Add(tagNameTextBox);
        panel.Children.Add(tagTypeComboBox);
        var tagButtons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        var addTagButton = new Button { Content = "Add Tag" };
        addTagButton.Click += async (_, _) => await AddTagAsync();
        tagButtons.Children.Add(addTagButton);
        panel.Children.Add(tagButtons);
        var writeMetadataButton = new Button { Content = "Write title and tags to image" };
        writeMetadataButton.Click += async (_, _) => await WriteImageMetadataAsync();
        panel.Children.Add(writeMetadataButton);

        panel.Children.Add(new TextBlock { Text = "Nearby Dates", FontWeight = FontWeight.SemiBold });
        nearbyListBox.Height = 180;
        panel.Children.Add(nearbyListBox);
        panel.Children.Add(new TextBlock { Text = "Similar / Related", FontWeight = FontWeight.SemiBold });
        relatedListBox.Height = 240;
        panel.Children.Add(relatedListBox);
        panel.Children.Add(new TextBlock { Text = "Mark as duplicate of", FontWeight = FontWeight.SemiBold });
        panel.Children.Add(canonicalComboBox);
        var duplicateButtons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        var markDuplicateButton = new Button { Content = "Mark Duplicate" };
        markDuplicateButton.Click += async (_, _) => await MarkDuplicateAsync();
        var hideButton = new Button { Content = "Hide" };
        hideButton.Click += async (_, _) => await HideAsync();
        duplicateButtons.Children.Add(markDuplicateButton);
        duplicateButtons.Children.Add(hideButton);
        panel.Children.Add(duplicateButtons);
        return new Border
        {
            Background = UiTheme.PanelBackground,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(12),
            Child = new ScrollViewer { Content = panel }
        };
    }

    private async Task LoadDetailsAsync()
    {
        details = await repository.GetDetailsAsync(photo.Id);
        if (details is null)
        {
            return;
        }

        var current = details.Photo;
        titleTextBox.Text = current.Title ?? string.Empty;
        dateTextBox.Text = current.InferredTakenDate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        currentTagsListBox.ItemsSource = details.Tags;
        nearbyListBox.ItemsSource = details.NearbyPhotos;
        relatedListBox.ItemsSource = details.RelatedPhotos;
        duplicateListBox.ItemsSource = details.DuplicateGroup;
        metadataTextBlock.Text = BuildMetadataText(details);
        LoadPreview(current);
    }

    private async Task CorrectDateAsync()
    {
        if (!DateTimeOffset.TryParse(dateTextBox.Text, out var date))
        {
            setStatus("Invalid date.");
            return;
        }

        await repository.CorrectTakenDateAsync(photo.Id, date, string.Empty);
        await LoadDetailsAsync();
        setStatus("Date saved.");
    }

    private async Task SaveTitleAsync()
    {
        await repository.UpdateTitleAsync(photo.Id, titleTextBox.Text);
        await LoadDetailsAsync();
        setStatus("Title saved.");
    }

    private async Task AddTagAsync()
    {
        if (string.IsNullOrWhiteSpace(tagNameTextBox.Text))
        {
            return;
        }

        var tagType = tagTypeComboBox.SelectedItem is TagType selected ? selected : TagType.Custom;
        await repository.AddTagAsync(photo.Id, tagNameTextBox.Text, tagType);
        tagNameTextBox.Text = string.Empty;
        await LoadDetailsAsync();
        setStatus("Tag added.");
    }

    private Control BuildTagListItem(Tag tag)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Margin = new Avalonia.Thickness(4)
        };
        row.Children.Add(new TextBlock
        {
            Text = tag.Name,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        var removeButton = new Button
        {
            Content = "Remove",
            Padding = new Avalonia.Thickness(8, 2)
        };
        removeButton.Click += async (_, _) => await RemoveTagAsync(tag);
        row.Children.Add(removeButton);
        Grid.SetColumn(removeButton, 1);
        return row;
    }

    private async Task RemoveTagAsync(Tag tag)
    {
        await repository.RemoveTagAsync(photo.Id, tag.Id);
        await LoadDetailsAsync();
        setStatus("Tag removed.");
    }

    private async Task WriteImageMetadataAsync()
    {
        try
        {
            await repository.WriteImageMetadataAsync(photo.Id);
            setStatus("Title and tags written to image.");
        }
        catch (NotSupportedException ex)
        {
            setStatus(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            setStatus(ex.Message);
        }
    }

    private async Task MarkDuplicateAsync()
    {
        if (canonicalComboBox.SelectedItem is not ReviewPhoto canonical || canonical.Id == photo.Id)
        {
            return;
        }

        await repository.MarkDuplicateAsync(photo.Id, canonical.Id);
        await LoadDetailsAsync();
        setStatus("Duplicate marked.");
    }

    private async Task HideAsync()
    {
        await repository.HideAsync(photo.Id);
        setStatus("Hidden.");
        await hiddenAsync();
    }

    private void LoadPreview(ReviewPhoto current)
    {
        previewImage.Source = null;
        var path = current.CurrentPath ?? current.OriginalPath;
        if (!File.Exists(path) || current.MediaKind is not MediaKind.SupportedImage)
        {
            previewContent.Content = BuildPreviewPlaceholder("No preview", path);
            return;
        }

        try
        {
            previewImage.Source = PhotoPreviewControls.LoadPreviewBitmap(current, decodeWidth: 1200);
            previewContent.Content = previewImage.Source is null
                ? BuildPreviewPlaceholder("Preview unavailable", path)
                : previewImage;
        }
        catch
        {
            previewImage.Source = null;
            previewContent.Content = BuildPreviewPlaceholder("Preview unavailable", path);
        }
    }

    private static Control BuildPreviewPlaceholder(string title, string path)
    {
        return new Border
        {
            Background = UiTheme.Border,
            MinHeight = 300,
            Child = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = Path.GetFileName(path),
                        Foreground = UiTheme.SecondaryText,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
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
            $"Title: {details.Photo.Title ?? "-"}",
            $"Date: {details.Photo.DisplayDate}",
            $"Confidence: {details.Photo.DateConfidence}",
            $"Camera: {metadata?.CameraMake ?? string.Empty} {metadata?.CameraModel ?? string.Empty}".Trim(),
            $"Size: {metadata?.Width?.ToString() ?? "-"} x {metadata?.Height?.ToString() ?? "-"}",
            $"Hash: {details.Photo.Sha256Hash ?? "-"}"
        });
    }
}
