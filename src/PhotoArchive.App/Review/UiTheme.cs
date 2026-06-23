using Avalonia.Media;

namespace PhotoArchive.App.Review;

internal static class UiTheme
{
    public static readonly IBrush AppBackground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
    public static readonly IBrush PanelBackground = Brushes.White;
    public static readonly IBrush SubtleBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
    public static readonly IBrush Border = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    public static readonly IBrush PrimaryText = new SolidColorBrush(Color.FromRgb(15, 23, 42));
    public static readonly IBrush SecondaryText = new SolidColorBrush(Color.FromRgb(71, 85, 105));
    public static readonly IBrush SelectedBackground = new SolidColorBrush(Color.FromRgb(191, 219, 254));
}
