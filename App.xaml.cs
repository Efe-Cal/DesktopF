using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopF;

public partial class App : Application
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private SearchWindow? searchWindow;
    private IntPtr previousForegroundWindow;
    private IntPtr foregroundHook;
    private WinEventDelegate? foregroundChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        searchWindow = new SearchWindow();

        DesktopHotkey.PressedCtrlF += OnDesktopCtrlF;
        DesktopHotkey.PressedEsc += OnDesktopEsc;
        DesktopHotkey.Start();

        foregroundChanged = OnForegroundChanged;
        foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            foregroundChanged,
            0,
            0,
            WINEVENT_OUTOFCONTEXT
        );

        if (foregroundHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }


    
    private void OnForegroundChanged(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    )
    {
        GetWindowThreadProcessId(windowHandle, out uint processId);

        if (
            DesktopHotkey.IsDesktopWindow(windowHandle) ||
            processId == Environment.ProcessId
        )
        {
            return;
        }

        Dispatcher.BeginInvoke(DismissWindows);
    }




    private void DismissWindows()
    {
        foreach (ShowWindow window in Windows.OfType<ShowWindow>().ToArray())
            window.Close();

        if (searchWindow?.IsVisible == true)
            searchWindow.Hide();
    }

    private void OnDesktopCtrlF()
    {
        Dispatcher.Invoke(() =>
        {
            if (searchWindow is null)
                return;

            IntPtr foregroundWindow = GetForegroundWindow();
            previousForegroundWindow = foregroundWindow;
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

                searchWindow.UpdateLayout();
                Rect workArea = SystemParameters.WorkArea;
                searchWindow.Left = workArea.Left + (workArea.Width - searchWindow.ActualWidth) / 2;
                searchWindow.Top = workArea.Top + (workArea.Height - searchWindow.ActualHeight) / 3;

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
            }
            else if (searchWindow?.IsVisible == true)
            {
                searchWindow.Hide();
            }
            else
            {
                return;
            }

            SetForegroundWindow(previousForegroundWindow);
        });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        IntPtr processId
    );

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId
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

    private delegate void WinEventDelegate(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr moduleHandle,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags
    );

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    protected override void OnExit(ExitEventArgs e)
    {
        DesktopHotkey.PressedCtrlF -= OnDesktopCtrlF;
        DesktopHotkey.PressedEsc -= OnDesktopEsc;
        DesktopHotkey.Stop();

        if (foregroundHook != IntPtr.Zero)
            UnhookWinEvent(foregroundHook);

        base.OnExit(e);
    }
}
