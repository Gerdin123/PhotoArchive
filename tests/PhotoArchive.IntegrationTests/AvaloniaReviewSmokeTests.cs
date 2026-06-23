using Avalonia.Controls;
using Avalonia.Styling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class AvaloniaReviewSmokeTests
{
    [Fact]
    public void Application_uses_light_theme_for_high_contrast_on_light_surfaces()
    {
        var application = new PhotoArchiveApplication();

        application.Initialize();

        Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
    }

    [Fact]
    public void SetupPage_can_return_to_folder_selection_after_error_without_reusing_visual_parent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-ui-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var setupPage = new SetupPage(
                Path.Combine(root, "photoarchive.db"),
                _ => Task.CompletedTask,
                _ => { },
                new DirectorySetupSettingsStore(Path.Combine(root, "settings.json")));

            var method = typeof(SetupPage).GetMethod(
                "ShowErrorStage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            method.Invoke(setupPage, [new InvalidOperationException("Expected failure")]);
            var errorView = Assert.IsType<ScrollViewer>(setupPage.Content);
            var panel = Assert.IsType<StackPanel>(errorView.Content);
            var backButton = panel.Children.OfType<Button>().Single(button => Equals(button.Content, "Back to Folders"));

            backButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.IsType<ScrollViewer>(setupPage.Content);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Review_pages_construct_and_initialize_for_directory_home_and_edit_flow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-ui-smoke-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var fileId = Guid.NewGuid();
            var tiffId = Guid.NewGuid();
            var tiffPath = Path.Combine(root, "scan.tif");
            await File.WriteAllBytesAsync(tiffPath, new byte[] { 0x49, 0x49, 0x2a, 0x00 });
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.AddRange(
                    new ArchiveFile
                    {
                        Id = fileId,
                        OriginalPath = Path.Combine(root, "photo.jpg"),
                        CurrentPath = Path.Combine(root, "photo.jpg"),
                        OriginalFileName = "photo.jpg",
                        Extension = ".jpg",
                        FileSizeBytes = 1,
                        Sha256Hash = "hash",
                        MediaKind = MediaKind.SupportedImage,
                        Status = ArchiveFileStatus.Processed
                    },
                    new ArchiveFile
                    {
                        Id = tiffId,
                        OriginalPath = tiffPath,
                        CurrentPath = tiffPath,
                        OriginalFileName = "scan.tif",
                        Extension = ".tif",
                        FileSizeBytes = 4,
                        Sha256Hash = "tiff-hash",
                        MediaKind = MediaKind.SupportedImage,
                        Status = ArchiveFileStatus.Processed
                    });
                dbContext.PhotoMetadata.AddRange(
                    new PhotoMetadata
                    {
                        ArchiveFileId = fileId,
                        InferredTakenDate = new DateTimeOffset(2020, 1, 2, 3, 4, 0, TimeSpan.Zero),
                        DateConfidence = DateConfidence.High
                    },
                    new PhotoMetadata
                    {
                        ArchiveFileId = tiffId,
                        InferredTakenDate = new DateTimeOffset(2020, 1, 3, 3, 4, 0, TimeSpan.Zero),
                        DateConfidence = DateConfidence.High
                    });
                await dbContext.SaveChangesAsync();
            }

            var setupOpened = false;
            var statuses = new List<string>();
            var setupPage = new SetupPage(
                databasePath,
                result =>
                {
                    setupOpened = result.FileCount > 0;
                    return Task.CompletedTask;
                },
                statuses.Add,
                new DirectorySetupSettingsStore(Path.Combine(root, "settings.json")));

            Assert.IsType<ScrollViewer>(setupPage.Content);
            Assert.False(setupOpened);

            var repository = new PhotoReviewRepository(databasePath);
            ReviewPhoto? selectedPhoto = null;
            var homePage = new HomePage(repository, statuses.Add, photo => selectedPhoto = photo, _ => Task.CompletedTask);
            await homePage.InitializeAsync();

            Assert.Equal(2, homePage.CurrentPhotos.Count);
            var photo = Assert.Single(homePage.CurrentPhotos, item => item.Id == fileId);
            var tiffPhoto = Assert.Single(homePage.CurrentPhotos, item => item.Id == tiffId);
            Assert.IsType<Grid>(homePage.Content);
            var advancedFiltersPanel = GetPrivateControl<WrapPanel>(homePage, "advancedFiltersPanel");
            var noTagsCheckBox = GetPrivateControl<CheckBox>(homePage, "noTagsCheckBox");
            Assert.False(advancedFiltersPanel.IsVisible);
            Assert.Equal("No tags", noTagsCheckBox.Content);
            Assert.Contains(noTagsCheckBox, advancedFiltersPanel.Children);

            var editPage = new EditPage(repository, photo, homePage.CurrentPhotos, statuses.Add, () => Task.CompletedTask);
            await editPage.InitializeAsync();
            var tiffEditPage = new EditPage(repository, tiffPhoto, homePage.CurrentPhotos, statuses.Add, () => Task.CompletedTask);
            await tiffEditPage.InitializeAsync();

            var editGrid = Assert.IsType<Grid>(editPage.Content);
            Assert.IsType<Grid>(tiffEditPage.Content);
            Assert.Contains(editGrid.Children, child => child is GridSplitter);
            Assert.Null(typeof(EditPage).GetField(
                "correctionReasonTextBox",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            Assert.NotNull(GetPrivateControl<TextBox>(editPage, "titleTextBox"));
            Assert.Equal(180, GetPrivateControl<ListBox>(editPage, "nearbyListBox").Height);
            Assert.Equal(240, GetPrivateControl<ListBox>(editPage, "relatedListBox").Height);
            Assert.NotNull(GetPrivateControl<ListBox>(editPage, "currentTagsListBox").ItemTemplate);
            Assert.Contains("Mark as duplicate of", GetTextBlocks(editPage).Select(textBlock => textBlock.Text));
            Assert.Contains("Write title and tags to image", GetButtons(editPage).Select(button => button.Content));
            Assert.NotEmpty(statuses);
            Assert.Null(selectedPhoto);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static TControl GetPrivateControl<TControl>(object instance, string fieldName)
        where TControl : Control
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return Assert.IsType<TControl>(field.GetValue(instance));
    }

    private static IReadOnlyList<TextBlock> GetTextBlocks(Control control)
    {
        var result = new List<TextBlock>();
        CollectTextBlocks(control, result);
        return result;
    }

    private static IReadOnlyList<Button> GetButtons(Control control)
    {
        var result = new List<Button>();
        CollectButtons(control, result);
        return result;
    }

    private static void CollectButtons(Control control, List<Button> result)
    {
        if (control is Button button)
        {
            result.Add(button);
        }

        switch (control)
        {
            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    CollectButtons(child, result);
                }

                break;
            case ContentControl { Content: Control content }:
                CollectButtons(content, result);
                break;
            case Decorator { Child: Control child }:
                CollectButtons(child, result);
                break;
        }
    }

    private static void CollectTextBlocks(Control control, List<TextBlock> result)
    {
        if (control is TextBlock textBlock)
        {
            result.Add(textBlock);
        }

        switch (control)
        {
            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    CollectTextBlocks(child, result);
                }

                break;
            case ContentControl { Content: Control content }:
                CollectTextBlocks(content, result);
                break;
            case Decorator { Child: Control child }:
                CollectTextBlocks(child, result);
                break;
        }
    }
}
