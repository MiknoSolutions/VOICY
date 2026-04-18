using System.Runtime.InteropServices;
using System.Windows;
using Voicy.Helpers;

namespace Voicy.Services.Input;

public sealed class ClipboardTextInjectionService : ITextInjectionService
{
    private IntPtr _lastForegroundWindow;

    /// <summary>
    /// Call this BEFORE recording starts to remember which window had focus.
    /// </summary>
    public void CaptureForegroundWindow()
    {
        _lastForegroundWindow = NativeInterop.GetForegroundWindow();
    }

    public void InjectText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var targetWindow = _lastForegroundWindow;

        // Must run on STA thread for clipboard access
        var thread = new Thread(() => InjectOnStaThread(text, targetWindow));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(3));
    }

    private static void InjectOnStaThread(string text, IntPtr targetWindow)
    {
        // Save current clipboard
        IDataObject? previousClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
                previousClipboard = Clipboard.GetDataObject();
        }
        catch
        {
            // Clipboard may be locked by another process
        }

        try
        {
            // Restore focus to the window that was active before recording
            if (targetWindow != IntPtr.Zero)
            {
                NativeInterop.SetForegroundWindow(targetWindow);
                Thread.Sleep(100);
            }

            Clipboard.SetText(text);
            Thread.Sleep(50);
            SendCtrlV();
            Thread.Sleep(50);
        }
        finally
        {
            // Restore previous clipboard after a brief delay
            try
            {
                Thread.Sleep(100);
                if (previousClipboard != null && previousClipboard.GetDataPresent(DataFormats.Text))
                {
                    var prevText = previousClipboard.GetData(DataFormats.Text) as string;
                    if (prevText != null)
                        Clipboard.SetText(prevText);
                }
            }
            catch
            {
                // Best-effort restore
            }
        }
    }

    private static void SendCtrlV()
    {
        int size = Marshal.SizeOf<NativeInterop.INPUT>();

        var inputs = new NativeInterop.INPUT[]
        {
            // Ctrl down
            new()
            {
                type = NativeInterop.INPUT_KEYBOARD,
                U = new NativeInterop.INPUTUNION
                {
                    ki = new NativeInterop.KEYBDINPUT { wVk = NativeInterop.VK_CONTROL }
                }
            },
            // V down
            new()
            {
                type = NativeInterop.INPUT_KEYBOARD,
                U = new NativeInterop.INPUTUNION
                {
                    ki = new NativeInterop.KEYBDINPUT { wVk = NativeInterop.VK_V }
                }
            },
            // V up
            new()
            {
                type = NativeInterop.INPUT_KEYBOARD,
                U = new NativeInterop.INPUTUNION
                {
                    ki = new NativeInterop.KEYBDINPUT { wVk = NativeInterop.VK_V, dwFlags = NativeInterop.KEYEVENTF_KEYUP }
                }
            },
            // Ctrl up
            new()
            {
                type = NativeInterop.INPUT_KEYBOARD,
                U = new NativeInterop.INPUTUNION
                {
                    ki = new NativeInterop.KEYBDINPUT { wVk = NativeInterop.VK_CONTROL, dwFlags = NativeInterop.KEYEVENTF_KEYUP }
                }
            }
        };

        NativeInterop.SendInput((uint)inputs.Length, inputs, size);
    }
}
