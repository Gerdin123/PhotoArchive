using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PhotoArchive.App.Diagnostics;

namespace PhotoArchive.App.Review;

public sealed class SetupPage : UserControl
{
    private static readonly string[] ProgressPhases =
    [
        "Force clean",
        "Preparing",
        "Scanning",
        "Analyzing",
        "Planning",
        "Writing manifest",
        "Copying",
        "Writing final manifest",
        "Thumbnails"
    ];

    private readonly TextBox inputPathTextBox = new();
    private readonly TextBox outputPathTextBox = new();
    private readonly TextBox databasePathTextBox = new();
    private readonly CheckBox forceCleanCheckBox = new()
    {
        Content = "Force-clean: discard the previous PhotoArchive database/output and preprocess again"
    };
    private readonly DirectorySetupSettingsStore settingsStore;
    private readonly Func<DirectorySetupResult, Task> openedAsync;
    private readonly Action<string> setStatus;
    private readonly Dictionary<string, ProgressBar> progressBars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> progressDetails = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBlock currentProgressTextBlock = new();
    private DirectorySetupResult? completedResult;

    public SetupPage(
        string databasePath,
        Func<DirectorySetupResult, Task> openedAsync,
        Action<string> setStatus,
        DirectorySetupSettingsStore? settingsStore = null)
    {
        this.openedAsync = openedAsync;
        this.setStatus = setStatus;
        this.settingsStore = settingsStore ?? new DirectorySetupSettingsStore();

        LoadInitialPaths(databasePath);
        ShowSelectionStage();
    }

    private void LoadInitialPaths(string databasePath)
    {
        var settings = settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.InputRoot))
        {
            inputPathTextBox.Text = settings.InputRoot;
            outputPathTextBox.Text = settings.OutputRoot;
            databasePathTextBox.Text = settings.DatabasePath ?? databasePath;
            return;
        }

        databasePathTextBox.Text = databasePath;
        var initialInputRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        inputPathTextBox.Text = initialInputRoot;
        if (!string.IsNullOrWhiteSpace(initialInputRoot))
        {
            var defaults = DirectorySetupDefaults.FromInputRoot(initialInputRoot);
            outputPathTextBox.Text = defaults.OutputRoot;
            databasePathTextBox.Text = defaults.DatabasePath;
        }
    }

    private void ShowSelectionStage()
    {
        DetachPathControls();
        var panel = BuildPagePanel();
        panel.Children.Add(BuildStageHeader(
            "1. Select Directories",
            "Choose the original folder, cleaned output folder, and local database."));
        panel.Children.Add(new TextBlock { Text = $"Logs: {AppLog.Current.LogDirectory}" });
        panel.Children.Add(LabeledPathControl("Original folder", inputPathTextBox, "Browse...", BrowseInputFolderAsync));
        panel.Children.Add(LabeledPathControl("Cleaned output folder", outputPathTextBox, "Browse...", BrowseOutputFolderAsync));
        panel.Children.Add(LabeledPathControl("SQLite database", databasePathTextBox, "Choose...", ChooseDatabasePathAsync));
        panel.Children.Add(forceCleanCheckBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Force-clean removes only PhotoArchive-managed output folders and the selected SQLite database. Original and cleaned folders must not overlap.",
            Foreground = Brushes.DarkOrange,
            TextWrapping = TextWrapping.Wrap
        });

        var startButton = new Button { Content = "Start" };
        startButton.Click += async (_, _) => await OpenOrPreprocessAsync();
        panel.Children.Add(startButton);

        Content = new ScrollViewer { Content = panel };
    }

    private void DetachPathControls()
    {
        DetachFromParent(inputPathTextBox);
        DetachFromParent(outputPathTextBox);
        DetachFromParent(databasePathTextBox);
        DetachFromParent(forceCleanCheckBox);
    }

    private static void DetachFromParent(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
            return;
        }

        if (control.Parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, control))
        {
            contentControl.Content = null;
            return;
        }

        if (control.Parent is Decorator decorator && ReferenceEquals(decorator.Child, control))
        {
            decorator.Child = null;
        }
    }

    private void ShowProgressStage()
    {
        progressBars.Clear();
        progressDetails.Clear();

        var panel = BuildPagePanel();
        panel.Children.Add(BuildStageHeader(
            "2. Progress",
            "Preprocessing is running. Each action reports its own progress and details."));
        currentProgressTextBlock.Text = "Waiting to start...";
        currentProgressTextBlock.FontWeight = FontWeight.SemiBold;
        currentProgressTextBlock.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(currentProgressTextBlock);

        foreach (var phase in ProgressPhases)
        {
            panel.Children.Add(BuildProgressRow(phase));
        }

        Content = new ScrollViewer { Content = panel };
    }

    private void ShowSummaryStage(DirectorySetupResult result)
    {
        completedResult = result;
        var panel = BuildPagePanel();
        panel.Children.Add(BuildStageHeader(
            "3. Summary",
            "Directory setup completed. Review the paths and continue to the archive home view."));

        panel.Children.Add(new TextBlock { Text = result.Message, FontSize = 16, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Images left: {result.ImagesLeft}" });
        panel.Children.Add(new TextBlock { Text = $"Duplicates: {result.Duplicates}" });
        panel.Children.Add(new TextBlock { Text = $"Unsupported files: {result.UnsupportedFiles}" });
        panel.Children.Add(new TextBlock { Text = $"Total time: {FormatDuration(result.Elapsed)}" });
        panel.Children.Add(new TextBlock { Text = $"Original folder: {inputPathTextBox.Text}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"Cleaned output folder: {result.OutputRoot}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"SQLite database: {result.DatabasePath}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"Logs: {AppLog.Current.LogDirectory}", TextWrapping = TextWrapping.Wrap });

        var homeButton = new Button { Content = "Open Home" };
        homeButton.Click += async (_, _) =>
        {
            if (completedResult is not null)
            {
                await openedAsync(completedResult);
            }
        };
        panel.Children.Add(homeButton);

        Content = new ScrollViewer { Content = panel };
    }

    private void ShowErrorStage(Exception exception)
    {
        var panel = BuildPagePanel();
        panel.Children.Add(BuildStageHeader(
            "Directory Setup Failed",
            "No files were intentionally changed after this failure point. Review the message and adjust the selected paths."));
        panel.Children.Add(new TextBlock
        {
            Text = exception.Message,
            Foreground = Brushes.DarkRed,
            TextWrapping = TextWrapping.Wrap
        });

        var backButton = new Button { Content = "Back to Folders" };
        backButton.Click += (_, _) => ShowSelectionStage();
        panel.Children.Add(backButton);
        Content = new ScrollViewer { Content = panel };
    }

    private async Task OpenOrPreprocessAsync()
    {
        try
        {
            setStatus("Opening directory...");
            settingsStore.Save(new DirectorySetupSettings(
                InputRoot: inputPathTextBox.Text,
                OutputRoot: outputPathTextBox.Text,
                DatabasePath: databasePathTextBox.Text));
            AppLog.Current.Info(nameof(SetupPage), $"Open/preprocess requested for input '{inputPathTextBox.Text}', output '{outputPathTextBox.Text}', database '{databasePathTextBox.Text}'.");
            ShowProgressStage();
            await Task.Yield();

            var progress = new Progress<DirectorySetupProgress>(ReportProgress);
            var inputRoot = inputPathTextBox.Text ?? string.Empty;
            var outputRoot = outputPathTextBox.Text ?? string.Empty;
            var databasePath = databasePathTextBox.Text ?? DirectorySetupDefaults.GetFallbackDatabasePath();
            var forceClean = forceCleanCheckBox.IsChecked == true;
            var result = await Task.Run(() => new DirectorySetupService().OpenOrPreprocessAsync(
                inputRoot,
                outputRoot,
                databasePath,
                progress,
                forceClean: forceClean));

            setStatus($"{result.Message} {result.FileCount} file(s).");
            ShowSummaryStage(result);
        }
        catch (Exception ex)
        {
            AppLog.Current.Error(nameof(SetupPage), "Open/preprocess failed.", ex);
            setStatus(ex.Message);
            ShowErrorStage(ex);
        }
    }

    private Control BuildProgressRow(string phase)
    {
        var panel = new Border
        {
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(10),
            Child = new StackPanel { Spacing = 4 }
        };

        var stack = (StackPanel)panel.Child!;
        stack.Children.Add(new TextBlock { Text = phase, FontWeight = FontWeight.SemiBold });
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 16
        };
        var detail = new TextBlock
        {
            Text = "Waiting",
            Foreground = UiTheme.SecondaryText,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(progressBar);
        stack.Children.Add(detail);
        progressBars[phase] = progressBar;
        progressDetails[phase] = detail;
        return panel;
    }

    private void ReportProgress(DirectorySetupProgress progress)
    {
        var phase = ProgressPhases.FirstOrDefault(known => known.Equals(progress.Phase, StringComparison.OrdinalIgnoreCase))
            ?? progress.Phase;
        var totalText = progress.TotalFiles?.ToString() ?? "?";
        var etaText = progress.EstimatedRemaining is null ? "unknown" : FormatDuration(progress.EstimatedRemaining.Value);
        currentProgressTextBlock.Text =
            $"{progress.Phase}: {progress.Message}{Environment.NewLine}" +
            $"Files found: {progress.FilesFound} | Processed: {progress.FilesProcessed}/{totalText} | Elapsed: {FormatDuration(progress.Elapsed)} | ETA: {etaText}";
        setStatus($"{progress.Phase}: {progress.Percentage:0.0}%");
        if (!progressBars.TryGetValue(phase, out var progressBar) || !progressDetails.TryGetValue(phase, out var detail))
        {
            return;
        }

        progressBar.Value = progress.Percentage;
        detail.Text =
            $"{progress.Message}{Environment.NewLine}" +
            $"Progress: {progress.Percentage:0.0}% | Files found: {progress.FilesFound} | Processed: {progress.FilesProcessed}/{totalText} | Elapsed: {FormatDuration(progress.Elapsed)} | ETA: {etaText}";
        detail.Foreground = UiTheme.PrimaryText;
    }

    private async Task BrowseInputFolderAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select original photo folder",
            AllowMultiple = false
        });

        var selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var defaults = DirectorySetupDefaults.FromInputRoot(selectedPath);
        inputPathTextBox.Text = defaults.InputRoot;
        outputPathTextBox.Text = defaults.OutputRoot;
        databasePathTextBox.Text = defaults.DatabasePath;
    }

    private async Task BrowseOutputFolderAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select cleaned output folder",
            AllowMultiple = false
        });

        var selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var previousOutputPath = outputPathTextBox.Text;
        outputPathTextBox.Text = Path.GetFullPath(selectedPath);
        if (string.IsNullOrWhiteSpace(databasePathTextBox.Text)
            || IsFallbackDatabasePath(databasePathTextBox.Text)
            || IsDatabaseUnderOutput(databasePathTextBox.Text, previousOutputPath))
        {
            databasePathTextBox.Text = Path.Combine(outputPathTextBox.Text, DirectorySetupDefaults.DatabaseFileName);
        }
    }

    private async Task ChooseDatabasePathAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Choose PhotoArchive database",
            SuggestedFileName = DirectorySetupDefaults.DatabaseFileName,
            DefaultExtension = "db",
            FileTypeChoices =
            [
                new FilePickerFileType("SQLite database")
                {
                    Patterns = ["*.db", "*.sqlite", "*.sqlite3"]
                }
            ]
        });

        var selectedPath = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            databasePathTextBox.Text = Path.GetFullPath(selectedPath);
        }
    }

    private static StackPanel BuildPagePanel()
    {
        return new StackPanel
        {
            Spacing = 12,
            Margin = new Avalonia.Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static Control BuildStageHeader(string title, string description)
    {
        return new Border
        {
            Background = UiTheme.SubtleBackground,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap }
                }
            }
        };
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

    private static Control LabeledPathControl(
        string label,
        TextBox textBox,
        string buttonText,
        Func<Task> browseAsync)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };

        var button = new Button { Content = buttonText };
        button.Click += async (_, _) => await browseAsync();
        row.Children.Add(textBox);
        Grid.SetColumn(textBox, 0);
        row.Children.Add(button);
        Grid.SetColumn(button, 1);

        return LabeledControl(label, row);
    }

    private static bool IsFallbackDatabasePath(string? databasePath)
    {
        return !string.IsNullOrWhiteSpace(databasePath)
            && string.Equals(
                Path.GetFullPath(databasePath),
                DirectorySetupDefaults.GetFallbackDatabasePath(),
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatabaseUnderOutput(string? databasePath, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        var fullDatabasePath = Path.GetFullPath(databasePath);
        var fullOutputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath)) + Path.DirectorySeparatorChar;
        return fullDatabasePath.StartsWith(fullOutputPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        return $"{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
