using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Vesper.Services.Recognition;

public sealed class OpenAiWhisperService : ISpeechRecognitionService
{
    private readonly HttpClient _httpClient = new();
    private string _apiKey = string.Empty;

    private const string Endpoint = "https://api.openai.com/v1/audio/transcriptions";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("API key not set.");

        using var content = new MultipartFormDataContent();

        var audioContent = new ByteArrayContent(wavAudio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");
        content.Add(new StringContent("whisper-1"), "model");

        if (!string.IsNullOrEmpty(language) && language != "auto")
        {
            content.Add(new StringContent(language), "language");
        }

        var response = await _httpClient.PostAsync(Endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
