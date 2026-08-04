using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopF;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DesktopItem> items;

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

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        string inputText = Input.Text.Trim();
        Input.Clear();
        if (string.IsNullOrEmpty(inputText))
        {
            return;
        }

        StringComparison comparison = CaseSensitiveOption.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        IEnumerable<DesktopItem> matches;
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
                MessageBox.Show("Invalid regular expression.", "DesktopF");
                return;
            }
        }
        else
        {
            matches = items.Where(item => StartsWithOption.IsChecked == true
                ? item.Name.StartsWith(inputText, comparison)
                : item.Name.Contains(inputText, comparison));
        }

        if (!matches.Any())
        {
            MessageBox.Show("No matching desktop items.", "DesktopF");
            return;
        }

        foreach (DesktopItem match in matches)
        {
            new ShowWindow(match).Show();
        }

        Hide();
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
