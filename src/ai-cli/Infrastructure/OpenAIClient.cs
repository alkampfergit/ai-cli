using AiCli.Application;
using AiCli.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiCli.Infrastructure;

/// <summary>
/// OpenAI-compatible API client implementation
/// </summary>
public class OpenAIClient : IAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIClient> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the OpenAIClient class
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <param name="baseUrl">The base URL for the API (optional)</param>
    public OpenAIClient(HttpClient httpClient, ILogger<OpenAIClient> logger, string apiKey, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ai-cli/1.0");
    }

    /// <inheritdoc/>
    public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = CreateRequestBody(request);
            var httpContent = new StringContent(requestBody, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending request to OpenAI API");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", httpContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API request failed with status {StatusCode}: {Response}", response.StatusCode, responseBody);
                return new AIResponse
                {
                    Content = "",
                    Model = request.Model,
                    RawResponse = responseBody,
                    Success = false,
                    ErrorMessage = $"API request failed with status {response.StatusCode}"
                };
            }

            var jsonResponse = JsonNode.Parse(responseBody);
            var responseContent = jsonResponse?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            var model = jsonResponse?["model"]?.ToString() ?? request.Model;

            _logger.LogDebug("Received response from OpenAI API");

            return new AIResponse
            {
                Content = responseContent,
                Model = model,
                RawResponse = responseBody,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending request to OpenAI API");
            return new AIResponse
            {
                Content = "",
                Model = request.Model,
                RawResponse = "",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SendStreamingRequestAsync(AIRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = CreateRequestBody(request with { Stream = true });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending streaming request to OpenAI API");

        using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Streaming API request failed with status {StatusCode}: {Response}", response.StatusCode, errorBody);
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (line.StartsWith("data: "))
            {
                var data = line[6..];
                if (data == "[DONE]")
                    break;

                string? delta = null;
                try
                {
                    var jsonData = JsonNode.Parse(data);
                    delta = jsonData?["choices"]?[0]?["delta"]?["content"]?.ToString();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse streaming response chunk: {Data}", data);
                    continue;
                }

                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }
            }
        }
    }

    private static string CreateRequestBody(AIRequest request)
    {
        var requestObj = new
        {
            model = request.Model,
            messages = new[]
            {
                new { role = "user", content = request.Prompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            stream = request.Stream
        };

        return JsonSerializer.Serialize(requestObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}