namespace Vesper.Services.Input;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyToggled;
    event EventHandler? HotkeyPressed;
    event EventHandler? HotkeyReleased;

    event EventHandler? Hotkey2Toggled;
    event EventHandler? Hotkey2Pressed;
    event EventHandler? Hotkey2Released;

    void SetHotkey(int modifiers, int key);
    void SetHotkey2(int modifiers, int key);
    void Start();
    void Stop();
}
