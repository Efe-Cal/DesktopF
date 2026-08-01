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

        Loaded += PointAtItem;
    }

    private void PointAtItem(object sender, RoutedEventArgs e)
    {
        System.Windows.Point location = item.HasScreenPosition
            ? PointFromScreen(new System.Windows.Point(item.ScreenX, item.ScreenY))
            : new System.Windows.Point(item.ViewX, item.ViewY);

        Canvas.SetLeft(Pointer, location.X - Pointer.ActualWidth / 2);
        Canvas.SetTop(Pointer, location.Y - Pointer.ActualHeight);
    }
}
