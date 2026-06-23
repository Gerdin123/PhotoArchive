using Avalonia.Controls;

namespace PhotoArchive.App.Review;

public sealed class SuggestionTextBox : UserControl
{
    private readonly TextBox textBox = new();
    private readonly ListBox suggestionsListBox = new();
    private IReadOnlyList<string> values = [];

    public SuggestionTextBox()
    {
        var panel = new StackPanel { Spacing = 2 };
        textBox.TextChanged += (_, _) => UpdateSuggestions();
        suggestionsListBox.MaxHeight = 120;
        suggestionsListBox.IsVisible = false;
        suggestionsListBox.SelectionChanged += (_, _) =>
        {
            if (suggestionsListBox.SelectedItem is string selected)
            {
                textBox.Text = selected;
                textBox.CaretIndex = selected.Length;
                suggestionsListBox.SelectedItem = null;
                suggestionsListBox.IsVisible = false;
            }
        };

        panel.Children.Add(textBox);
        panel.Children.Add(suggestionsListBox);
        Content = panel;
    }

    public string? Text
    {
        get => textBox.Text;
        set
        {
            textBox.Text = value;
            UpdateSuggestions();
        }
    }

    public string? PlaceholderText
    {
        get => textBox.PlaceholderText;
        set => textBox.PlaceholderText = value;
    }

    public void SetValues(IEnumerable<int> newValues)
    {
        values = newValues
            .Select(value => value.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        var text = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            suggestionsListBox.IsVisible = false;
            suggestionsListBox.ItemsSource = null;
            return;
        }

        var matches = values
            .Where(value => value.StartsWith(text, StringComparison.OrdinalIgnoreCase) && !value.Equals(text, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();
        suggestionsListBox.ItemsSource = matches;
        suggestionsListBox.IsVisible = matches.Count > 0;
    }
}
