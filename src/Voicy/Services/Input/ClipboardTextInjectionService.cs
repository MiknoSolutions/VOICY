using System.Runtime.InteropServices;
using System.Windows;
using Voicy.Helpers;

namespace Voicy.Services.Input;

public sealed class ClipboardTextInjectionService : ITextInjectionService
{
    private IntPtr _lastForegroundWindow;

    public void CaptureForegroundWindow()
    {
        _lastForegroundWindow = NativeInterop.GetForegroundWindow();
    }

    public void InjectText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var targetWindow = _lastForegroundWindow;

        // Step 1: Set clipboard on the WPF dispatcher thread (has message pump)
        Application.Current.Dispatcher.Invoke(() =>
        {
            try { Clipboard.SetText(text); }
            catch { /* clipboard locked */ }
        });

        // Step 2: Focus target window and paste on a background thread
        // (to avoid blocking the UI with Thread.Sleep)
        Task.Run(() =>
        {
            try
            {
                // Release modifier keys first (hotkey combo ghost state)
                ReleaseAllModifiers();
                Thread.Sleep(50);

                // Focus the target window
                FocusWindow(targetWindow);

                // Send Ctrl+V
                SendCtrlV();
            }
            catch { /* best effort */ }
        });
    }

    private static void FocusWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero) return;

        var currentThreadId = NativeInterop.GetCurrentThreadId();
        var targetThreadId = NativeInterop.GetWindowThreadProcessId(targetWindow, out _);

        bool attached = false;
        if (currentThreadId != targetThreadId)
            attached = NativeInterop.AttachThreadInput(currentThreadId, targetThreadId, true);

        try
        {
            NativeInterop.SetForegroundWindow(targetWindow);
            NativeInterop.BringWindowToTop(targetWindow);
        }
        finally
        {
            if (attached)
                NativeInterop.AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        Thread.Sleep(200);
    }

    private static void ReleaseAllModifiers()
    {
        int size = Marshal.SizeOf<NativeInterop.INPUT>();

        ushort[] modifiers =
        [
            NativeInterop.VK_LCONTROL, NativeInterop.VK_RCONTROL,
            NativeInterop.VK_LSHIFT,   NativeInterop.VK_RSHIFT,
            NativeInterop.VK_LMENU,    NativeInterop.VK_RMENU,
        ];

        var inputs = new NativeInterop.INPUT[modifiers.Length];
        for (int i = 0; i < modifiers.Length; i++)
        {
            inputs[i] = new NativeInterop.INPUT
            {
                type = NativeInterop.INPUT_KEYBOARD,
                U = new NativeInterop.INPUTUNION
                {
                    ki = new NativeInterop.KEYBDINPUT
                    {
                        wVk = modifiers[i],
                        dwFlags = NativeInterop.KEYEVENTF_KEYUP
                    }
                }
            };
        }

        NativeInterop.SendInput((uint)inputs.Length, inputs, size);
    }

    private static void SendCtrlV()
    {
        int size = Marshal.SizeOf<NativeInterop.INPUT>();

        var inputs = new NativeInterop.INPUT[]
        {
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_LCONTROL } } },
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_V } } },
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_V, dwFlags = NativeInterop.KEYEVENTF_KEYUP } } },
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_LCONTROL, dwFlags = NativeInterop.KEYEVENTF_KEYUP } } },
        };

        NativeInterop.SendInput((uint)inputs.Length, inputs, size);
    }
}
