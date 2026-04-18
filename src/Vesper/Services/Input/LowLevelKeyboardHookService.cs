using System.Diagnostics;
using System.Runtime.InteropServices;
using Vesper.Helpers;

namespace Vesper.Services.Input;

public sealed class LowLevelKeyboardHookService : IGlobalHotkeyService
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeInterop.LowLevelKeyboardProc? _hookProc;

    private int _targetModifiers; // bitmask: 1=Alt, 2=Ctrl, 4=Shift, 8=Win
    private int _targetKey;

    private int _target2Modifiers;
    private int _target2Key;

    private bool _isKeyDown;
    private bool _isKey2Down;
    private int _currentModifiers;

    public event EventHandler? HotkeyToggled;
    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public event EventHandler? Hotkey2Toggled;
    public event EventHandler? Hotkey2Pressed;
    public event EventHandler? Hotkey2Released;

    public void SetHotkey(int modifiers, int key)
    {
        _targetModifiers = modifiers;
        _targetKey = key;
    }

    public void SetHotkey2(int modifiers, int key)
    {
        _target2Modifiers = modifiers;
        _target2Key = key;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeInterop.SetWindowsHookEx(
            NativeInterop.WH_KEYBOARD_LL,
            _hookProc,
            NativeInterop.GetModuleHandle(curModule.ModuleName),
            0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeInterop.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int msg = wParam.ToInt32();
            bool isDown = msg == NativeInterop.WM_KEYDOWN || msg == NativeInterop.WM_SYSKEYDOWN;
            bool isUp = msg == NativeInterop.WM_KEYUP || msg == NativeInterop.WM_SYSKEYUP;

            UpdateModifiers(vkCode, isDown);

            if (vkCode == _targetKey && _currentModifiers == _targetModifiers)
            {
                if (isDown && !_isKeyDown)
                {
                    _isKeyDown = true;
                    HotkeyToggled?.Invoke(this, EventArgs.Empty);
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
                else if (isUp && _isKeyDown)
                {
                    _isKeyDown = false;
                    HotkeyReleased?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (vkCode == _target2Key && _currentModifiers == _target2Modifiers && _target2Key != 0)
            {
                if (isDown && !_isKey2Down)
                {
                    _isKey2Down = true;
                    Hotkey2Toggled?.Invoke(this, EventArgs.Empty);
                    Hotkey2Pressed?.Invoke(this, EventArgs.Empty);
                }
                else if (isUp && _isKey2Down)
                {
                    _isKey2Down = false;
                    Hotkey2Released?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (IsModifierKey(vkCode) && isUp)
            {
                if (_isKeyDown)
                {
                    _isKeyDown = false;
                    HotkeyReleased?.Invoke(this, EventArgs.Empty);
                }
                if (_isKey2Down)
                {
                    _isKey2Down = false;
                    Hotkey2Released?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return NativeInterop.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void UpdateModifiers(int vkCode, bool isDown)
    {
        int flag = vkCode switch
        {
            0xA4 or 0xA5 => 0x01, // Alt (L/R)
            0xA2 or 0xA3 => 0x02, // Ctrl (L/R)
            0xA0 or 0xA1 => 0x04, // Shift (L/R)
            0x5B or 0x5C => 0x08, // Win (L/R)
            _ => 0
        };

        if (flag == 0) return;

        if (isDown)
            _currentModifiers |= flag;
        else
            _currentModifiers &= ~flag;
    }

    private static bool IsModifierKey(int vkCode) =>
        vkCode is (>= 0xA0 and <= 0xA5) or 0x5B or 0x5C;

    public void Dispose() => Stop();
}
