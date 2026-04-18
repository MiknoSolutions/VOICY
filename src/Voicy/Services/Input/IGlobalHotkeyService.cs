namespace Voicy.Services.Input;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyToggled;
    event EventHandler? HotkeyPressed;
    event EventHandler? HotkeyReleased;
    void SetHotkey(int modifiers, int key);
    void Start();
    void Stop();
}
