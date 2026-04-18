using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Vesper.Helpers;

namespace Vesper.Services.Input;

public sealed class ClipboardTextInjectionService : ITextInjectionService
{
    private IntPtr _lastForegroundWindow;

    public void CaptureForegroundWindow()
    {
        _lastForegroundWindow = NativeInterop.GetForegroundWindow();
    }

    public void InjectText(string text, bool sendEnter = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        var targetWindow = _lastForegroundWindow;

        // Set clipboard on WPF dispatcher thread (needs message pump)
        Application.Current.Dispatcher.Invoke(() =>
        {
            try { Clipboard.SetText(text); }
            catch { /* clipboard may be locked */ }
        });

        // Schedule paste with a delay to let clipboard settle.
        // Use DispatcherTimer so we stay on the UI thread (which owns a window
        // and can interact with the input system reliably).
        var timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            // If VESPER window somehow stole focus, give it back
            var currentFg = NativeInterop.GetForegroundWindow();
            if (currentFg != targetWindow && targetWindow != IntPtr.Zero)
            {
                NativeInterop.SetForegroundWindow(targetWindow);
                Thread.Sleep(100);
            }

            // Release any modifier keys left from the hotkey combo
            ReleaseAllModifiers();
            Thread.Sleep(30);

            // Send Ctrl+V to paste
            SendCtrlV();

            if (sendEnter)
            {
                Thread.Sleep(50);
                SendEnter();
            }
        };
        timer.Start();
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

    private static void SendEnter()
    {
        int size = Marshal.SizeOf<NativeInterop.INPUT>();

        var inputs = new NativeInterop.INPUT[]
        {
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_RETURN } } },
            new() { type = NativeInterop.INPUT_KEYBOARD, U = new() { ki = new() { wVk = NativeInterop.VK_RETURN, dwFlags = NativeInterop.KEYEVENTF_KEYUP } } },
        };

        NativeInterop.SendInput((uint)inputs.Length, inputs, size);
    }
}
