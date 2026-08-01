using System.Windows;
using System.Windows.Input;

namespace DesktopF;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DesktopItem> items;

    public SearchWindow()
    {
        InitializeComponent();
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
        if (string.IsNullOrEmpty(inputText))
        {
            return;
        }

        IEnumerable<DesktopItem> matches = items.Where(item =>
            item.Name.Contains(inputText, StringComparison.OrdinalIgnoreCase));

        if (!matches.Any())
        {
            MessageBox.Show("No matching desktop items.", "DesktopF");
            return;
        }

        foreach (DesktopItem match in matches)
        {
            new ShowWindow(match).Show();
        }

        Close();
    }
}
