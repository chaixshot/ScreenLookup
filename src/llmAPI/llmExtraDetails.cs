using ScreenLookup.src.utils;
using System.Text;
using System.Text.Json;

namespace ScreenLookup.src.llmAPI
{
    public class llmExtraDetails
    {
        private static readonly HttpClient client = new();
        private static readonly int openai_fallback_index = 0;

        public static async Task<string> GetExtraDetails(ProviderService provider, string text, CancellationToken token = default)
        {
            string targetLangName = LanguageList.GetDisplayNameFromID(App.setting.TargetLanguage, false);
            BaseLLMConfig config = provider switch
            {
                ProviderService.OpenAI => App.setting.OpenAIConfig,
                ProviderService.Ollama => App.setting.OllamaConfig,
                ProviderService.OpenRouter => App.setting.OpenRouterConfig,
                ProviderService.LMStudio => App.setting.LMStudioConfig,
                _ => throw new NotSupportedException($"Provider {provider} does not support extra details.")
            };

            string systemPrompt = config.ExtraDetailSystemPrompt;
            string prompt = string.Format(config.ExtraDetailPrompt, text, targetLangName);

            return provider switch
            {
                ProviderService.OpenAI => await OpenAI(systemPrompt, prompt, token),
                ProviderService.Ollama => await Ollama(systemPrompt, prompt, token),
                ProviderService.OpenRouter => await OpenRouter(systemPrompt, prompt, token),
                ProviderService.LMStudio => await LMStudio(systemPrompt, prompt, token),
                _ => throw new NotSupportedException($"Provider {provider} does not support extra details.")
            };
        }

        private static async Task<string> OpenAI(string systemPrompt, string userPrompt, CancellationToken token)
        {
            var config = App.setting.OpenAIConfig;
            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? OpenAIConfig.DefaultUrl : config.ApiUrl;
            string url = baseUri.EndsWith("/chat/completions") ? baseUri : $"{baseUri.TrimEnd('/')}/chat/completions";

            var requestData = llmRequestDataFactory.Create(openai_fallback_index, config.ModelName, messages, config.Temperature);
            string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.SendAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    throw;
                throw new Exception("Request timeout");
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync(token);
                var responseObj = JsonSerializer.Deserialize<OpenAIConfig.Response>(responseString);
                return responseObj?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> Ollama(string systemPrompt, string userPrompt, CancellationToken token)
        {
            var config = App.setting.OllamaConfig;
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? OllamaConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = $"{baseUri.TrimEnd('/')}/api/chat";

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var requestData = llmRequestDataFactory.Create("Ollama", config.ModelName, messages, config.Temperature);
            string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.SendAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    throw;
                throw new Exception("Request timeout");
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync(token);
                var responseObj = JsonSerializer.Deserialize<OllamaConfig.Response>(responseString);
                return responseObj?.Message?.Content ?? string.Empty;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> OpenRouter(string systemPrompt, string userPrompt, CancellationToken token)
        {
            var config = App.setting.OpenRouterConfig;
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? OpenRouterConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = baseUri.EndsWith("/chat/completions") ? baseUri : $"{baseUri.TrimEnd('/')}/chat/completions";

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var requestData = llmRequestDataFactory.Create("OpenRouter", config.ModelName, messages, config.Temperature);
            string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.SendAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    throw;
                throw new Exception("Request timeout");
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync(token);
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> LMStudio(string systemPrompt, string userPrompt, CancellationToken token)
        {
            var config = App.setting.LMStudioConfig;
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? LMStudioConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = $"{baseUri.TrimEnd('/')}/chat";

            var requestData = new
            {
                model = config.ModelName,
                system_prompt = systemPrompt,
                input = userPrompt,
                temperature = config.Temperature
            };

            string jsonContent = JsonSerializer.Serialize(requestData);
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.SendAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    throw;
                throw new Exception("Request timeout");
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync(token);
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                if (root.TryGetProperty("output", out var outputArray) && outputArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in outputArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "message" &&
                            item.TryGetProperty("content", out var contentProp))
                        {
                            return contentProp.GetString() ?? string.Empty;
                        }
                    }
                }
                return responseString;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }
    }
}
