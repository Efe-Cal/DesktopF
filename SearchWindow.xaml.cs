using System.Windows;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace DesktopF;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DesktopItem> items;

    public SearchWindow()
    {
        InitializeComponent();
        Width = SystemParameters.PrimaryScreenWidth * 0.4;
        Input.Focus();
        items = DesktopReader.GetItems();
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
}
