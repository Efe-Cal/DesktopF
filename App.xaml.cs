using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopF;

public partial class App : Application
{
    private SearchWindow? searchWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        searchWindow = new SearchWindow();

        DesktopHotkey.PressedCtrlF += OnDesktopCtrlF;
        DesktopHotkey.PressedEsc += OnDesktopEsc;
        DesktopHotkey.Start();
    }

    private void OnDesktopCtrlF()
    {
        Dispatcher.Invoke(() =>
        {
            if (searchWindow is null)
                return;

            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(
                foregroundWindow,
                IntPtr.Zero
            );
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != currentThread &&
                AttachThreadInput(currentThread, foregroundThread, true);

            try
            {
                if (!searchWindow.IsVisible)
                    searchWindow.Show();

                if (searchWindow.WindowState == WindowState.Minimized)
                    searchWindow.WindowState = WindowState.Normal;

                SetForegroundWindow(new WindowInteropHelper(searchWindow).Handle);
                searchWindow.Activate();
                Keyboard.Focus(searchWindow.Input);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, foregroundThread, false);
            }
        });
    }

    private void OnDesktopEsc()
    {
        Dispatcher.Invoke(() =>
        {
            ShowWindow[] showWindows = Windows
                .OfType<ShowWindow>()
                .ToArray();
            
            if (showWindows.Length > 0)
            {
                foreach (ShowWindow window in showWindows)
                    window.Close();

                return;
            }

            if (searchWindow?.IsVisible == true)
                searchWindow.Hide();
        });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        IntPtr processId
    );

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(
        uint attachThread,
        uint attachToThread,
        bool attach
    );

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    protected override void OnExit(ExitEventArgs e)
    {
        DesktopHotkey.PressedCtrlF -= OnDesktopCtrlF;
        DesktopHotkey.PressedEsc -= OnDesktopEsc;
        DesktopHotkey.Stop();

        base.OnExit(e);
    }
}
