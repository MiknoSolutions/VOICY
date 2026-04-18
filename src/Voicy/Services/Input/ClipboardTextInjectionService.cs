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

        var thread = new Thread(() => InjectOnStaThread(text, targetWindow));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5));
    }

    private static void InjectOnStaThread(string text, IntPtr targetWindow)
    {
        // Save current clipboard
        string? previousText = null;
        try
        {
            if (Clipboard.ContainsText())
                previousText = Clipboard.GetText();
        }
        catch { }

        try
        {
            // Forcefully restore focus to the target window
            FocusWindow(targetWindow);

            // Release any held modifier keys (from hotkey combo) before pasting
            ReleaseAllModifiers();
            Thread.Sleep(30);

            Clipboard.SetText(text);
            Thread.Sleep(50);
            SendCtrlV();
            Thread.Sleep(100);
        }
        finally
        {
            // Best-effort restore clipboard
            try
            {
                if (previousText != null)
                {
                    Thread.Sleep(200);
                    Clipboard.SetText(previousText);
                }
            }
            catch { }
        }
    }

    private static void FocusWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero) return;

        // Attach our thread's input to the target window's thread
        // so SetForegroundWindow works even when we're not foreground
        var currentThreadId = NativeInterop.GetCurrentThreadId();
        var targetThreadId = NativeInterop.GetWindowThreadProcessId(targetWindow, out _);

        bool attached = false;
        if (currentThreadId != targetThreadId)
            attached = NativeInterop.AttachThreadInput(currentThreadId, targetThreadId, true);

        try
        {
            NativeInterop.BringWindowToTop(targetWindow);
            NativeInterop.SetForegroundWindow(targetWindow);
        }
        finally
        {
            if (attached)
                NativeInterop.AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        // Give the OS time to actually switch focus
        Thread.Sleep(150);
    }

    private static void ReleaseAllModifiers()
    {
        int size = Marshal.SizeOf<NativeInterop.INPUT>();

        // Release Ctrl, Shift, Alt (both sides) to clear ghost key state
        ushort[] modifiers =
        [
            NativeInterop.VK_LCONTROL,
            NativeInterop.VK_RCONTROL,
            NativeInterop.VK_LSHIFT,
            NativeInterop.VK_RSHIFT,
            NativeInterop.VK_LMENU,
            NativeInterop.VK_RMENU,
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
