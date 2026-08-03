using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopF;

public static class DesktopHotkey
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private const int VK_CONTROL = 0x11;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F = 0x46;

    private static IntPtr _hookHandle = IntPtr.Zero;

    private static readonly LowLevelKeyboardProc HookProcedure = HookCallback;

    public static event Action? PressedCtrlF;
    public static event Action? PressedEsc;

    public static void Start()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        using Process process = Process.GetCurrentProcess();
        using ProcessModule? module = process.MainModule;

        if (module is null)
            throw new InvalidOperationException(
                "Could not get the current process module."
            );

        IntPtr moduleHandle = GetModuleHandle(module.ModuleName);

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            HookProcedure,
            moduleHandle,
            0
        );

        if (_hookHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private static IntPtr HookCallback(
        int code,
        IntPtr wParam,
        IntPtr lParam
    )
    {
        if (code >= 0 && wParam == WM_KEYDOWN)
        {
            int virtualKey = Marshal.ReadInt32(lParam);

            if (virtualKey == VK_ESCAPE)
                PressedEsc?.Invoke();

            bool controlPressed =
                (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            if (
                virtualKey == VK_F &&
                controlPressed &&
                IsDesktopForeground()
            )
            {
                PressedCtrlF?.Invoke();

                // Prevent Windows Explorer from processing Ctrl+F.
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(
            _hookHandle,
            code,
            wParam,
            lParam
        );
    }

    private static bool IsDesktopForeground()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        Console.WriteLine($"Foreground window handle: {foregroundWindow}");

        return IsDesktopWindow(foregroundWindow);
    }

    internal static bool IsDesktopWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
            return false;

        var className = new StringBuilder(256);

        GetClassName(
            windowHandle,
            className,
            className.Capacity
        );

        string windowClass = className.ToString();
        Console.WriteLine($"Foreground window class: {windowClass}");
        Console.WriteLine($"Is desktop foreground: {windowClass is "Progman" or "WorkerW"}");
        return windowClass is "Progman" or "WorkerW";
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int code,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hookHandle
    );

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int code,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int virtualKey
    );

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode
    )]
    private static extern int GetClassName(
        IntPtr windowHandle,
        StringBuilder className,
        int maximumCount
    );

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode
    )]
    private static extern IntPtr GetModuleHandle(
        string? moduleName
    );
}
