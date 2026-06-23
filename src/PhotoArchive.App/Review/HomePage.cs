using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed class HomePage : UserControl
{
    private const int PageSize = 24;

    private readonly PhotoReviewRepository repository;
    private readonly Action<string> setStatus;
    private readonly Action<ReviewPhoto?> selectionChanged;
    private readonly Func<ReviewPhoto?, Task> editAsync;

    private readonly TextBox searchTextBox = new();
    private readonly SuggestionTextBox yearTextBox = new();
    private readonly SuggestionTextBox decadeTextBox = new();
    private readonly ComboBox statusComboBox = new();
    private readonly ComboBox tagPickerComboBox = new();
    private readonly WrapPanel activeTagsPanel = new();
    private readonly ComboBox sortComboBox = new();
    private readonly CheckBox noTagsCheckBox = new();
    private readonly CheckBox duplicatesOnlyCheckBox = new();
    private readonly CheckBox includeDuplicatesCheckBox = new();
    private readonly CheckBox includeUnsupportedCheckBox = new();
    private readonly CheckBox includeDeletedCheckBox = new();
    private readonly CheckBox needsReviewCheckBox = new();
    private readonly WrapPanel advancedFiltersPanel = new();
    private readonly WrapPanel photoGridPanel = new();
    private readonly TextBlock pageTextBlock = new();
    private ReviewPhoto? selectedPhoto;
    private int pageNumber = 1;
    private bool configured;
    private IReadOnlyList<TagOption> tagOptions = [];
    private readonly List<TagOption> selectedTags = [];

    public HomePage(
        PhotoReviewRepository repository,
        Action<string> setStatus,
        Action<ReviewPhoto?> selectionChanged,
        Func<ReviewPhoto?, Task> editAsync)
    {
        this.repository = repository;
        this.setStatus = setStatus;
        this.selectionChanged = selectionChanged;
        this.editAsync = editAsync;

        Content = Build();
    }

    public IReadOnlyList<ReviewPhoto> CurrentPhotos { get; private set; } = [];
    public ReviewPhoto? SelectedPhoto => selectedPhoto;

    public async Task InitializeAsync()
    {
        await repository.InitializeAsync();
        if (!configured)
        {
            ConfigureControls();
            configured = true;
        }

        await LoadDateSuggestionsAsync();
        await LoadTagFilterAsync();
        await LoadPhotoPageAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadDateSuggestionsAsync();
        await LoadTagFilterAsync();
        await LoadPhotoPageAsync();
    }

    private Control Build()
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(16)
        };

        panel.Children.Add(BuildFilterBar());
        Grid.SetRow(panel.Children[^1], 0);
        photoGridPanel.Margin = new Avalonia.Thickness(0, 4, 0, 0);
        var scrollViewer = new ScrollViewer
        {
            Content = photoGridPanel,
            Background = UiTheme.SubtleBackground
        };
        panel.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 1);
        panel.Children.Add(BuildPaginationBar());
        Grid.SetRow(panel.Children[^1], 2);
        return panel;
    }

    private void ConfigureControls()
    {
        statusComboBox.ItemsSource = new object[] { "Any status" }.Concat(Enum.GetValues<ArchiveFileStatus>().Cast<object>()).ToArray();
        statusComboBox.SelectedIndex = statusComboBox.SelectedIndex < 0 ? 0 : statusComboBox.SelectedIndex;
        tagPickerComboBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(TagOption.DisplayName));
        tagPickerComboBox.SelectionChanged += (_, _) =>
        {
            if (tagPickerComboBox.SelectedItem is not TagOption tag)
            {
                return;
            }

            if (selectedTags.All(selected => selected.Id != tag.Id))
            {
                selectedTags.Add(tag);
                RenderActiveTags();
                RefreshAvailableTagOptions();
            }

            tagPickerComboBox.SelectedItem = null;
        };
        sortComboBox.ItemsSource = Enum.GetValues<ReviewSortMode>();
        sortComboBox.SelectedItem ??= ReviewSortMode.DateAscending;
    }

    private Control BuildFilterBar()
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };
        var primaryPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        searchTextBox.PlaceholderText = "Search";
        searchTextBox.Width = 180;
        yearTextBox.PlaceholderText = "Year";
        yearTextBox.Width = 96;
        decadeTextBox.PlaceholderText = "Decade";
        decadeTextBox.Width = 118;
        statusComboBox.Width = 150;
        sortComboBox.Width = 150;
        noTagsCheckBox.Content = "No tags";
        duplicatesOnlyCheckBox.Content = "Duplicates";
        includeDuplicatesCheckBox.Content = "Include duplicates";
        includeUnsupportedCheckBox.Content = "Include unsupported";
        includeDeletedCheckBox.Content = "Include hidden";
        needsReviewCheckBox.Content = "Needs review";

        AddFilterControl(primaryPanel, searchTextBox);
        AddFilterControl(primaryPanel, yearTextBox);
        AddFilterControl(primaryPanel, decadeTextBox);
        AddFilterControl(primaryPanel, BuildTagFilterControl(), width: 270);
        AddFilterControl(primaryPanel, sortComboBox);

        var applyButton = new Button { Content = "Apply" };
        applyButton.Click += async (_, _) =>
        {
            pageNumber = 1;
            await LoadPhotoPageAsync();
        };
        AddFilterControl(primaryPanel, applyButton);

        advancedFiltersPanel.Orientation = Orientation.Horizontal;
        advancedFiltersPanel.IsVisible = false;
        AddFilterControl(advancedFiltersPanel, noTagsCheckBox);
        AddFilterControl(advancedFiltersPanel, statusComboBox);
        AddFilterControl(advancedFiltersPanel, duplicatesOnlyCheckBox);
        AddFilterControl(advancedFiltersPanel, includeDuplicatesCheckBox);
        AddFilterControl(advancedFiltersPanel, includeUnsupportedCheckBox);
        AddFilterControl(advancedFiltersPanel, includeDeletedCheckBox);
        AddFilterControl(advancedFiltersPanel, needsReviewCheckBox);

        var advancedButton = new Button { Content = "Advanced filters" };
        advancedButton.Click += (_, _) =>
        {
            advancedFiltersPanel.IsVisible = !advancedFiltersPanel.IsVisible;
            advancedButton.Content = advancedFiltersPanel.IsVisible ? "Hide advanced filters" : "Advanced filters";
        };

        panel.Children.Add(primaryPanel);
        panel.Children.Add(advancedButton);
        panel.Children.Add(advancedFiltersPanel);

        return new Border
        {
            Background = UiTheme.PanelBackground,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(12),
            Child = panel
        };
    }

    private static void AddFilterControl(Panel panel, Control control, double? width = null)
    {
        if (width is not null)
        {
            control.Width = width.Value;
        }

        control.Margin = new Avalonia.Thickness(0, 0, 8, 8);
        panel.Children.Add(control);
    }

    private Control BuildTagFilterControl()
    {
        var panel = new StackPanel { Spacing = 4 };
        tagPickerComboBox.PlaceholderText = "Add tag filter";
        activeTagsPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        panel.Children.Add(tagPickerComboBox);
        panel.Children.Add(activeTagsPanel);
        return panel;
    }

    private void RefreshAvailableTagOptions()
    {
        var selectedIds = selectedTags.Select(tag => tag.Id).ToHashSet();
        tagPickerComboBox.ItemsSource = tagOptions
            .Where(tag => !selectedIds.Contains(tag.Id))
            .ToList();
    }

    private void RenderActiveTags()
    {
        activeTagsPanel.Children.Clear();
        foreach (var tag in selectedTags.ToArray())
        {
            var chip = new Border
            {
                BorderBrush = UiTheme.Border,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(6, 2),
                Margin = new Avalonia.Thickness(0, 0, 4, 4)
            };
            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4
            };
            row.Children.Add(new TextBlock { Text = tag.DisplayName });
            var removeButton = new Button
            {
                Content = "x",
                Padding = new Avalonia.Thickness(4, 0)
            };
            removeButton.Click += (_, _) =>
            {
                selectedTags.RemoveAll(selected => selected.Id == tag.Id);
                RenderActiveTags();
                RefreshAvailableTagOptions();
            };
            row.Children.Add(removeButton);
            chip.Child = row;
            activeTagsPanel.Children.Add(chip);
        }
    }

    private Control BuildPaginationBar()
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
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
        editButton.Click += async (_, _) => await editAsync(SelectedPhoto);

        panel.Children.Add(previousButton);
        panel.Children.Add(nextButton);
        panel.Children.Add(editButton);
        panel.Children.Add(pageTextBlock);
        return panel;
    }

    private async Task LoadDateSuggestionsAsync()
    {
        yearTextBox.SetValues(await repository.GetAvailableYearsAsync());
        decadeTextBox.SetValues(await repository.GetAvailableDecadesAsync());
    }

    private async Task LoadTagFilterAsync()
    {
        var selectedIds = selectedTags.Select(tag => tag.Id).ToHashSet();
        var tags = await repository.GetTagOptionsAsync();
        tagOptions = tags;
        selectedTags.RemoveAll(tag => !tags.Any(option => option.Id == tag.Id));
        foreach (var tag in tags.Where(tag => selectedIds.Contains(tag.Id) && selectedTags.All(selected => selected.Id != tag.Id)))
        {
            selectedTags.Add(tag);
        }

        RefreshAvailableTagOptions();
        RenderActiveTags();
    }

    private async Task LoadPhotoPageAsync()
    {
        var page = await repository.GetPhotoPageAsync(BuildFilter(), pageNumber, PageSize);
        if (page.PageNumber > page.TotalPages)
        {
            pageNumber = page.TotalPages;
            page = await repository.GetPhotoPageAsync(BuildFilter(), pageNumber, PageSize);
        }

        CurrentPhotos = page.Photos;
        RenderPhotoGrid();
        pageTextBlock.Text = BuildPageText(page);
        setStatus(pageTextBlock.Text);
    }

    private static string BuildPageText(ReviewPhotoPage page)
    {
        return $"Page {page.PageNumber} of {page.TotalPages} ({page.TotalCount} visible; "
            + $"{page.Summary.SupportedImages} supported, "
            + $"{page.Summary.DuplicateFiles} duplicates hidden by default, "
            + $"{page.Summary.UnsupportedFiles} unsupported hidden by default, "
            + $"{page.Summary.DeletedFiles} hidden/deleted)";
    }

    private void RenderPhotoGrid()
    {
        photoGridPanel.Children.Clear();
        if (selectedPhoto is not null && CurrentPhotos.All(photo => photo.Id != selectedPhoto.Id))
        {
            selectedPhoto = null;
            selectionChanged(null);
        }

        foreach (var photo in CurrentPhotos)
        {
            var button = new Button
            {
                Content = PhotoPreviewControls.BuildPhotoCard(photo),
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(0),
                Background = photo.Id == selectedPhoto?.Id
                    ? UiTheme.SelectedBackground
                    : Avalonia.Media.Brushes.Transparent
            };
            button.Click += (_, _) =>
            {
                selectedPhoto = photo;
                selectionChanged(photo);
                RenderPhotoGrid();
            };
            button.DoubleTapped += async (_, _) => await editAsync(photo);
            photoGridPanel.Children.Add(button);
        }
    }

    private ReviewFilter BuildFilter()
    {
        var status = statusComboBox.SelectedItem is ArchiveFileStatus selectedStatus ? selectedStatus : (ArchiveFileStatus?)null;
        var tagIds = selectedTags
            .Select(tag => tag.Id)
            .ToArray();
        var (from, to) = ResolveDateFilter();
        return new ReviewFilter(
            SearchText: searchTextBox.Text,
            Status: status,
            TagIds: tagIds,
            NoTagsOnly: noTagsCheckBox.IsChecked == true,
            DuplicatesOnly: duplicatesOnlyCheckBox.IsChecked == true,
            IncludeDuplicates: includeDuplicatesCheckBox.IsChecked == true,
            IncludeUnsupported: includeUnsupportedCheckBox.IsChecked == true,
            IncludeDeleted: includeDeletedCheckBox.IsChecked == true,
            UncertainOrUnprocessedOnly: needsReviewCheckBox.IsChecked == true,
            From: from,
            To: to,
            SortMode: sortComboBox.SelectedItem is ReviewSortMode sortMode ? sortMode : ReviewSortMode.DateAscending);
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
}
