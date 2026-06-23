using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoArchive.App.Diagnostics;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public static class PhotoPreviewControls
{
    public static Control BuildPhotoListItem(ReviewPhoto photo)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("112,*"),
            ColumnSpacing = 10,
            Margin = new Avalonia.Thickness(4)
        };

        grid.Children.Add(BuildImageFrame(photo, width: 104, height: 78, decodeWidth: 180));
        Grid.SetColumn(grid.Children[^1], 0);

        var text = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.Children.Add(new TextBlock
        {
            Text = photo.DisplayDate,
            FontWeight = FontWeight.SemiBold
        });
        text.Children.Add(new TextBlock { Text = $"{photo.Status} | {photo.DateConfidence}" });
        if (!string.IsNullOrWhiteSpace(photo.Tags))
        {
            text.Children.Add(new TextBlock { Text = photo.Tags, TextWrapping = TextWrapping.Wrap });
        }

        text.Children.Add(new TextBlock
        {
            Text = photo.DisplayName,
            Foreground = UiTheme.SecondaryText,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });

        grid.Children.Add(text);
        Grid.SetColumn(text, 1);
        return grid;
    }

    public static Control BuildPhotoCard(ReviewPhoto photo)
    {
        var panel = new StackPanel
        {
            Width = 180,
            Spacing = 4
        };
        panel.Children.Add(BuildImageFrame(photo, width: 168, height: 126, decodeWidth: 260));
        panel.Children.Add(new TextBlock
        {
            Text = photo.DisplayName,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42
        });
        panel.Children.Add(new TextBlock
        {
            Text = photo.DisplayDate,
            FontSize = 11,
            Foreground = UiTheme.SecondaryText
        });
        if (!string.IsNullOrWhiteSpace(photo.Tags))
        {
            panel.Children.Add(new TextBlock
            {
                Text = photo.Tags,
                FontSize = 11,
                Foreground = UiTheme.SecondaryText,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 34
            });
        }

        return new Border
        {
            Width = 190,
            Margin = new Avalonia.Thickness(6),
            Padding = new Avalonia.Thickness(8),
            Background = UiTheme.PanelBackground,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Child = panel
        };
    }

    public static Control BuildRelatedPhotoListItem(RelatedReviewPhoto related)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(BuildPhotoListItem(related.Photo));
        panel.Children.Add(new TextBlock
        {
            Text = related.Reasons,
            Foreground = UiTheme.SecondaryText,
            FontSize = 11,
            Margin = new Avalonia.Thickness(116, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    public static Bitmap? LoadPreviewBitmap(ReviewPhoto photo, int decodeWidth)
    {
        var path = !string.IsNullOrWhiteSpace(photo.ThumbnailPath) && File.Exists(photo.ThumbnailPath)
            ? photo.ThumbnailPath
            : photo.CurrentPath ?? photo.OriginalPath;
        if (!File.Exists(path) || photo.MediaKind is not MediaKind.SupportedImage)
        {
            return null;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or NullReferenceException
            or ArgumentException
            or InvalidOperationException)
        {
            AppLog.Current.Warning(nameof(PhotoPreviewControls), $"Could not decode preview image '{path}': {ex.Message}");
            return null;
        }
    }

    private static Control BuildImageFrame(ReviewPhoto photo, double width, double height, int decodeWidth)
    {
        var bitmap = LoadPreviewBitmap(photo, decodeWidth);
        if (bitmap is null)
        {
            return new Border
            {
                Width = width,
                Height = height,
                Background = UiTheme.Border,
                CornerRadius = new Avalonia.CornerRadius(6),
                Child = new TextBlock
                {
                    Text = "No preview",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = UiTheme.SecondaryText
                }
            };
        }

        var image = new Image
        {
            Source = bitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.UniformToFill
        };

        return new Border
        {
            Width = width,
            Height = height,
            Background = UiTheme.Border,
            CornerRadius = new Avalonia.CornerRadius(6),
            Child = image
        };
    }
}
