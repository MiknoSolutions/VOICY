using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Voicy.Services.Recognition;

/// <summary>
/// Connects to a local ASR server using the OpenAI-compatible /v1/audio/transcriptions API format.
/// Works with faster-whisper-server, whisper.cpp --server, LocalAI, or any compatible server.
/// </summary>
public sealed class LocalApiService : ISpeechRecognitionService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
    private string _baseUrl = string.Empty;
    private string _modelName = string.Empty;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_baseUrl);

    public void Configure(string baseUrl, string modelName = "")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _modelName = modelName;
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Local API URL not configured.");

        var endpoint = $"{_baseUrl}/v1/audio/transcriptions";

        using var content = new MultipartFormDataContent();

        var audioContent = new ByteArrayContent(wavAudio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");

        if (!string.IsNullOrWhiteSpace(_modelName))
            content.Add(new StringContent(_modelName), "model");

        if (!string.IsNullOrEmpty(language) && language != "auto")
            content.Add(new StringContent(language), "language");

        var response = await _httpClient.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        // Try OpenAI format first: { "text": "..." }
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
                return textProp.GetString() ?? string.Empty;
        }
        catch
        {
            // Not JSON — some servers return plain text
        }

        return json.Trim();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
