using System.Windows;
using System.Windows.Controls;

namespace DesktopF;

public partial class ShowWindow : Window
{
    private readonly DesktopItem item;

    public ShowWindow(DesktopItem item)
    {
        InitializeComponent();
        this.item = item;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += HighlightItem;
    }

    private void HighlightItem(object sender, RoutedEventArgs e)
    {
        const double margin = 3;
        const double fallbackWidth = 96;
        const double fallbackHeight = 72;

        System.Windows.Point topLeft = item.HasScreenPosition
            ? PointFromScreen(new System.Windows.Point(item.ScreenX, item.ScreenY))
            : new System.Windows.Point(item.ViewX, item.ViewY);

        System.Windows.Point bottomRight = item.HasScreenBounds
            ? PointFromScreen(new System.Windows.Point(
                item.ScreenX + item.ScreenWidth,
                item.ScreenY + item.ScreenHeight))
            : new System.Windows.Point(
                topLeft.X + fallbackWidth,
                topLeft.Y + fallbackHeight);

        Canvas.SetLeft(Highlight, topLeft.X - margin);
        Canvas.SetTop(Highlight, topLeft.Y - margin);
        Highlight.Width = bottomRight.X - topLeft.X + margin * 2;
        Highlight.Height = bottomRight.Y - topLeft.Y + margin * 2;
    }
}
