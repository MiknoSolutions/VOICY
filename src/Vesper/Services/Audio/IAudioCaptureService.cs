namespace Vesper.Services.Audio;

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<float[]>? DataAvailable;
    void PrewarmMicrophone();
    void StartRecording();
    byte[] StopRecording();
    byte[] FlushBuffer();
    List<string> GetAvailableDevices();
    void SetDevice(int deviceIndex);
}
