namespace Voicy.Services.Input;

public interface ITextInjectionService
{
    void CaptureForegroundWindow();
    void InjectText(string text);
}
