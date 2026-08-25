using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevBoard.Services;

public class OllamaMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class OllamaResponse
{
    public OllamaMessage? Message { get; set; }
    public bool Done { get; set; }
}

public class OllamaModel
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
}

public class OllamaTagsResponse
{
    public List<OllamaModel>? Models { get; set; }
}

public class OllamaService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string _model;

    public string Model => _model;

    public OllamaService(string model = "qwen2.5-coder:3b", string baseUrl = "http://localhost:11434")
    {
        _model = model;
        _baseUrl = baseUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public void SetModel(string model) => _model = model;

    public async Task<string> ChatAsync(List<OllamaMessage> messages)
    {
        var request = new
        {
            model = _model,
            messages = messages,
            stream = false
        };

        var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return result?.Message?.Content ?? "";
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(List<OllamaMessage> messages)
    {
        var request = new
        {
            model = _model,
            messages = messages,
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;

            OllamaResponse? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaResponse>(line);
            }
            catch { }

            if (chunk?.Message?.Content != null)
                yield return chunk.Message.Content;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModel>> GetModelsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
            return tags?.Models ?? new List<OllamaModel>();
        }
        catch
        {
            return new List<OllamaModel>();
        }
    }
}
