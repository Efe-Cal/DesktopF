using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopF;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DesktopItem> items = [];

    public SearchWindow()
    {
        InitializeComponent();
        Width = Math.Clamp(
                    SystemParameters.PrimaryScreenWidth * 0.4,
                    MinWidth,
                    MaxWidth);
        SourceInitialized += EnableNativeBackdrop;
        Input.Focus();
        items = DesktopReader.GetItems();
    }

    private void EnableNativeBackdrop(object? sender, EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int roundedCorners = 2;
        int acrylicBackdrop = 3;

        _ = DwmSetWindowAttribute(handle, 33, ref roundedCorners, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 38, ref acrylicBackdrop, sizeof(int));
    }

    private void SearchWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            if (ResultsList.Items.Count == 0)
            {
                return;
            }

            int selectedIndex = e.Key == Key.Down
                ? Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1)
                : Math.Max(ResultsList.SelectedIndex - 1, -1);

            ResultsList.SelectedIndex = selectedIndex;
            if (ResultsList.SelectedItem is not null)
            {
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }

            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        DesktopItem[] selectedItems = ResultsList.SelectedItem is DesktopItem selectedItem
            ? [selectedItem]
            : ResultsList.Items.Cast<DesktopItem>().ToArray();

        if (selectedItems.Length == 0)
        {
            return;
        }

        foreach (ShowWindow window in Application.Current.Windows
                     .OfType<ShowWindow>()
                     .ToArray())
        {
            window.Close();
        }

        Input.Clear();
        foreach (DesktopItem item in selectedItems)
        {
            new ShowWindow(item).Show();
        }

        Hide();
    }

    private void Input_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateMatches();
    }

    private void SearchOptions_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMatches();
    }

    private void UpdateMatches()
    {
        string inputText = Input.Text.Trim();
        if (string.IsNullOrEmpty(inputText))
        {
            ShowMatches([]);
            return;
        }

        StringComparison comparison = CaseSensitiveOption.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        IReadOnlyList<DesktopItem> matches;
        if (RegexOption.IsChecked == true)
        {
            try
            {
                string pattern = StartsWithOption.IsChecked == true ? $@"\A(?:{inputText})" : inputText;
                Regex regex = new(pattern, CaseSensitiveOption.IsChecked == true
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase);
                matches = items.Where(item => regex.IsMatch(item.Name)).ToList();
            }
            catch (ArgumentException)
            {
                ShowMatches([]);
                return;
            }
        }
        else
        {
            matches = items.Where(item => StartsWithOption.IsChecked == true
                ? item.Name.StartsWith(inputText, comparison)
                : item.Name.Contains(inputText, comparison)).ToList();
        }

        ShowMatches(matches);
    }

    private void ShowMatches(IReadOnlyList<DesktopItem> matches)
    {
        ResultsList.ItemsSource = matches;
        ResultsPanel.Visibility = matches.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        ResultsList.SelectedIndex = -1;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OptionsPanel.Visibility = OptionsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref int value,
        int valueSize);
}
