using System.Runtime.InteropServices;
using System.Text;

namespace DesktopF;

public sealed record DesktopItem(
    string Name,
    string? Path,
    string ParsingName,
    int ViewX,
    int ViewY,
    int ScreenX,
    int ScreenY,
    bool HasScreenPosition,
    int ScreenWidth,
    int ScreenHeight,
    bool HasScreenBounds);

public static class DesktopReader
{
    private const int SwcDesktop = 8;
    private const int SwfoNeedDispatch = 1;
    private const uint SvgioAllView = 0x00000002;

    private static readonly Guid SidSTopLevelBrowser =
        new("4C96BE40-915C-11CF-99D3-00AA004AE837");

    public static IReadOnlyList<DesktopItem> GetItems()
    {
        object? shellObject = null;
        object? windowsObject = null;
        object? desktopDispatch = null;
        object? browserObject = null;
        object? viewObject = null;

        try
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application")
                ?? throw new InvalidOperationException(
                    "Windows Shell.Application is not registered.");

            shellObject = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException(
                    "Could not create Windows Shell.Application.");

            dynamic shell = shellObject;
            windowsObject = shell.Windows;
            if (windowsObject is null)
            {
                throw new InvalidOperationException(
                    "Could not access the Windows Shell windows collection.");
            }

            dynamic shellWindows = windowsObject;

            int ignoredDesktopWindowHandle = 0;

            desktopDispatch = shellWindows.FindWindowSW(
                Type.Missing,
                Type.Missing,
                SwcDesktop,
                ref ignoredDesktopWindowHandle,
                SwfoNeedDispatch);

            if (desktopDispatch is null)
            {
                throw new InvalidOperationException(
                    "Explorer's desktop view could not be found.");
            }

            var serviceProvider = (IServiceProvider)desktopDispatch;

            browserObject = serviceProvider.QueryService(
                SidSTopLevelBrowser,
                typeof(IShellBrowser).GUID);

            var browser = (IShellBrowser)browserObject;
            IShellView shellView = browser.QueryActiveShellView();
            viewObject = shellView;

            var folderView = (IFolderView)viewObject;
            var folderView2 = (IFolderView2)viewObject;
            int getWindowResult = shellView.GetWindow(out IntPtr viewWindow);
            Marshal.ThrowExceptionForHR(getWindowResult);
            int count = folderView.ItemCount(SvgioAllView);
            var results = new List<DesktopItem>(count);

            for (int index = 0; index < count; index++)
            {
                IntPtr childPidl = IntPtr.Zero;
                IShellItem? shellItem = null;

                try
                {
                    childPidl = folderView.Item(index);
                    folderView.GetItemPosition(childPidl, out Point viewPoint);

                    shellItem = folderView2.GetItem(
                        index,
                        typeof(IShellItem).GUID);

                    string name = GetDisplayNameOrNull(
                        shellItem,
                        Sigdn.NormalDisplay)
                        ?? "<unknown>";


                    string? path = GetDisplayNameOrNull(
                        shellItem,
                        Sigdn.FileSystemPath);

                    string parsingName = GetDisplayNameOrNull(
                        shellItem,
                        Sigdn.DesktopAbsoluteParsing)
                        ?? name;

                    Point screenPoint = viewPoint;
                    bool converted = NativeMethods.ClientToScreen(
                        viewWindow,
                        ref screenPoint);

                    bool hasBounds = NativeMethods.TryGetListViewItemBounds(
                        viewWindow,
                        index,
                        out NativeRect itemBounds);
                    if (hasBounds)
                    {
                        screenPoint.X = itemBounds.Left;
                        screenPoint.Y = itemBounds.Top;
                    }

                    results.Add(new DesktopItem(
                        Name: name,
                        Path: path,
                        ParsingName: parsingName,
                        ViewX: viewPoint.X,
                        ViewY: viewPoint.Y,
                        ScreenX: screenPoint.X,
                        ScreenY: screenPoint.Y,
                        HasScreenPosition: converted || hasBounds,
                        ScreenWidth: itemBounds.Right - itemBounds.Left,
                        ScreenHeight: itemBounds.Bottom - itemBounds.Top,
                        HasScreenBounds: hasBounds));
                }
                finally
                {
                    if (childPidl != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(childPidl);
                    }

                    ReleaseComObject(shellItem);
                }
            }

            return results;
        }
        finally
        {
            ReleaseComObject(viewObject);
            ReleaseComObject(browserObject);
            ReleaseComObject(desktopDispatch);
            ReleaseComObject(windowsObject);
            ReleaseComObject(shellObject);
        }
    }

    private static string? GetDisplayNameOrNull(
        IShellItem item,
        Sigdn nameType)
    {
        try
        {
            return item.GetDisplayName(nameType);
        }
        catch (Exception exception)
            when (exception is COMException or ArgumentException)
        {
            return null;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}

internal static class NativeMethods
{
    private const uint LvmGetItemRect = 0x100E;
    private const uint ProcessVmAccess = 0x0038;
    private const uint MemCommitReserve = 0x3000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    internal static bool TryGetListViewItemBounds(
        IntPtr viewWindow,
        int itemIndex,
        out NativeRect bounds)
    {
        bounds = default;
        IntPtr listView = FindWindowEx(
            viewWindow,
            IntPtr.Zero,
            "SysListView32",
            null);

        if (listView == IntPtr.Zero)
        {
            listView = viewWindow;
        }

        _ = GetWindowThreadProcessId(listView, out uint processId);
        IntPtr process = OpenProcess(ProcessVmAccess, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        nuint size = (nuint)Marshal.SizeOf<NativeRect>();
        IntPtr remoteRect = VirtualAllocEx(
            process,
            IntPtr.Zero,
            size,
            MemCommitReserve,
            PageReadWrite);

        try
        {
            NativeRect requestedBounds = default;
            if (remoteRect == IntPtr.Zero ||
                !WriteProcessMemory(
                    process,
                    remoteRect,
                    ref requestedBounds,
                    size,
                    out _) ||
                SendMessage(
                    listView,
                    LvmGetItemRect,
                    (IntPtr)itemIndex,
                    remoteRect) == IntPtr.Zero ||
                !ReadProcessMemory(
                    process,
                    remoteRect,
                    out bounds,
                    size,
                    out _))
            {
                bounds = default;
                return false;
            }

            Point topLeft = new() { X = bounds.Left, Y = bounds.Top };
            Point bottomRight = new() { X = bounds.Right, Y = bounds.Bottom };
            if (!ClientToScreen(listView, ref topLeft) ||
                !ClientToScreen(listView, ref bottomRight))
            {
                bounds = default;
                return false;
            }

            bounds = new NativeRect
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = bottomRight.X,
                Bottom = bottomRight.Y
            };

            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
        }
        finally
        {
            if (remoteRect != IntPtr.Zero)
            {
                _ = VirtualFreeEx(process, remoteRect, 0, MemRelease);
            }

            _ = CloseHandle(process);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(
        IntPtr dpiContext);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(
        IntPtr windowHandle,
        ref Point point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr parentWindow,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess,
        bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        uint allocationType,
        uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(
        IntPtr process,
        IntPtr address,
        nuint size,
        uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(
        IntPtr process,
        IntPtr address,
        ref NativeRect buffer,
        nuint size,
        out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        out NativeRect buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    internal static void TryEnablePerMonitorDpiAwareness()
    {
        try
        {
            // This can fail if the process's DPI mode was already selected.
            // That is harmless for this console application.
            _ = SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows versions do not expose this function.
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal enum Sigdn : int
{
    NormalDisplay = 0x00000000,
    ParentRelativeParsing = unchecked((int)0x80018001),
    DesktopAbsoluteParsing = unchecked((int)0x80028000),
    ParentRelativeEditing = unchecked((int)0x80031001),
    DesktopAbsoluteEditing = unchecked((int)0x8004C000),
    FileSystemPath = unchecked((int)0x80058000),
    Url = unchecked((int)0x80068000),
    ParentRelativeForAddressBar = unchecked((int)0x8007C001),
    ParentRelative = unchecked((int)0x80080001),
    ParentRelativeForUi = unchecked((int)0x80094001)
}

[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProvider
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(
        [MarshalAs(UnmanagedType.LPStruct)] Guid service,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId);
}

[ComImport]
[Guid("000214E2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    // Skip the two IOleWindow and ten IShellBrowser methods before this one.
    void _VtblGap1_12();

    IShellView QueryActiveShellView();
}

[ComImport]
[Guid("000214E3-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    [PreserveSig]
    int GetWindow(out IntPtr windowHandle);
}

[ComImport]
[Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView
{
    // GetCurrentViewMode, SetCurrentViewMode, GetFolder
    void _VtblGap1_3();

    IntPtr Item(int itemIndex);

    int ItemCount(uint flags);

    // Items, GetSelectionMarkedItem, GetFocusedItem
    void _VtblGap2_3();

    void GetItemPosition(
        IntPtr childPidl,
        out Point point);
}

[ComImport]
[Guid("1AF3A467-214F-4298-908E-06B03E0B39F9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView2
{
    // Skip all 14 IFolderView methods and the first 12 IFolderView2 methods.
    void _VtblGap1_26();

    IShellItem GetItem(
        int itemIndex,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    // BindToHandler, GetParent
    void _VtblGap1_2();

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName(Sigdn nameType);
}
