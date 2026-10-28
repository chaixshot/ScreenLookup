using ScreenLookup.src.utils;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScreenLookup.src.llmAPI
{
    public static class llmTranslate
    {
        private static readonly HttpClient client = new();
        private static readonly int openai_fallback_index = 0;

        public static async Task<string> GetTranslate(ProviderService provider, string text, CancellationToken token = default)
        {
            return provider switch
            {
                ProviderService.OpenAI => await OpenAI(text, token),
                ProviderService.Ollama => await Ollama(text, token),
                ProviderService.LMStudio => await LMStudio(text, token),
                ProviderService.OpenRouter => await OpenRouter(text, token),
                ProviderService.DeepL => await DeepL(text, token),
                ProviderService.Youdao => await Youdao(text, token),
                ProviderService.MTranServer => await MTranServer(text, token),
                ProviderService.Baidu => await Baidu(text, token),
                ProviderService.LibreTranslate => await LibreTranslate(text, token),
                _ => throw new NotSupportedException($"Provider {provider} does not support translation.")
            };
        }

        private static async Task<string> OpenAI(string text, CancellationToken token = default)
        {
            var config = App.setting.OpenAIConfig;
            string language = LanguageList.GetDisplayNameFromID(App.setting.TargetLanguage, true);

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = string.Format(config.TranslatePrompt, language) },
                new() { Role = "user", Content = $"🔤 {text} 🔤" }
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
                var output = responseObj?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
                return output.Replace("🔤", "").Trim();
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> Ollama(string text, CancellationToken token = default)
        {
            var config = App.setting.OllamaConfig;
            string language = LanguageList.GetDisplayNameFromID(App.setting.TargetLanguage, true);
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? OllamaConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = $"{baseUri.TrimEnd('/')}/api/chat";

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = string.Format(config.TranslatePrompt, language) },
                new() { Role = "user", Content = $"🔤 {text} 🔤" }
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
                var output = responseObj?.Message?.Content ?? string.Empty;
                return output.Replace("🔤", "").Trim();
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> LMStudio(string text, CancellationToken token = default)
        {
            var config = App.setting.LMStudioConfig;
            string language = LanguageList.GetDisplayNameFromID(App.setting.TargetLanguage, true);
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? LMStudioConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = $"{baseUri.TrimEnd('/')}/chat";

            var requestData = new
            {
                model = config.ModelName,
                system_prompt = string.Format(config.TranslatePrompt, language),
                input = $"🔤 {text} 🔤",
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
                            return (contentProp.GetString() ?? "").Replace("🔤", "").Trim();
                        }
                    }
                }
                return responseString.Replace("🔤", "").Trim();
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> OpenRouter(string text, CancellationToken token = default)
        {
            var config = App.setting.OpenRouterConfig;
            string language = LanguageList.GetDisplayNameFromID(App.setting.TargetLanguage, true);
            string baseUri = string.IsNullOrEmpty(config.ApiUrl) ? OpenRouterConfig.DefaultUrl : config.ApiUrl;
            string apiUrl = baseUri.EndsWith("/chat/completions") ? baseUri : $"{baseUri.TrimEnd('/')}/chat/completions";

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { Role = "system", Content = string.Format(config.TranslatePrompt, language) },
                new() { Role = "user", Content = $"🔤 {text} 🔤" }
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
                var output = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                return output.Replace("🔤", "").Trim();
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> DeepL(string text, CancellationToken token = default)
        {
            var config = App.setting.DeepLConfig;
            string language = LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage);
            string apiUrl = string.IsNullOrEmpty(config.ApiUrl) ? DeepLConfig.DefaultUrl : config.ApiUrl;

            var requestData = new { text = new[] { text }, target_lang = language };
            string jsonContent = JsonSerializer.Serialize(requestData);

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("DeepL-Auth-Key", config.ApiKey);

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
                if (doc.RootElement.TryGetProperty("translations", out var translations) && translations.ValueKind == JsonValueKind.Array && translations.GetArrayLength() > 0)
                {
                    return translations[0].GetProperty("text").GetString() ?? string.Empty;
                }
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> Youdao(string text, CancellationToken token = default)
        {
            var config = App.setting.YoudaoConfig;
            string language = LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage);
            string salt = DateTime.Now.Millisecond.ToString();
            string sign = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes($"{config.AppKey}{text}{salt}{config.AppSecret}")));

            var parameters = new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = "auto",
                ["to"] = language,
                ["appKey"] = config.AppKey,
                ["salt"] = salt,
                ["sign"] = sign
            };

            var content = new FormUrlEncodedContent(parameters);
            string apiUrl = string.IsNullOrEmpty(config.ApiUrl) ? YoudaoConfig.DefaultUrl : config.ApiUrl;

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.PostAsync(apiUrl, content, cts.Token);
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
                var responseObj = JsonSerializer.Deserialize<YoudaoConfig.TranslationResult>(responseString);
                if (responseObj?.ErrorCode != "0" && responseObj?.ErrorCode != null)
                    throw new Exception($"Youdao Error - {responseObj.ErrorCode}");
                return responseObj?.Translation?.FirstOrDefault() ?? throw new Exception("No content");
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> MTranServer(string text, CancellationToken token = default)
        {
            var config = App.setting.MTranServerConfig;
            string sourceLanguage = LanguageList.GetLanguageISO6391FromID(App.setting.SourceLanguage);
            string targetLanguage = LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage);
            string apiUrl = string.IsNullOrEmpty(config.ApiUrl) ? MTranServerConfig.DefaultUrl : config.ApiUrl;

            var requestData = new { text, from = sourceLanguage, to = targetLanguage };
            string jsonContent = JsonSerializer.Serialize(requestData);

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
                string responseString = await response.Content.ReadAsStringAsync(token);
                var responseObj = JsonSerializer.Deserialize<MTranServerConfig.Response>(responseString);
                return responseObj?.Result ?? string.Empty;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> Baidu(string text, CancellationToken token = default)
        {
            var config = App.setting.BaiduConfig;
            string language = LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage);
            string salt = DateTime.Now.Millisecond.ToString();
            string sign = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes($"{config.AppId}{text}{salt}{config.AppSecret}")));

            var parameters = new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = "auto",
                ["to"] = language,
                ["appid"] = config.AppId,
                ["salt"] = salt,
                ["sign"] = sign
            };

            var content = new FormUrlEncodedContent(parameters);
            string apiUrl = string.IsNullOrEmpty(config.ApiUrl) ? BaiduConfig.DefaultUrl : config.ApiUrl;

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                response = await client.PostAsync(apiUrl, content, cts.Token);
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
                var responseObj = JsonSerializer.Deserialize<BaiduConfig.TranslationResult>(responseString);
                if (responseObj?.ErrorCode != null && responseObj.ErrorCode != "0")
                    throw new Exception($"Baidu Error - {responseObj.ErrorCode}");
                return responseObj?.TransResult?.FirstOrDefault()?.Dst ?? throw new Exception("No content");
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }

        private static async Task<string> LibreTranslate(string text, CancellationToken token = default)
        {
            var config = App.setting.LibreTranslateConfig;
            string targetLanguage = LanguageList.GetLanguageISO6391FromID(App.setting.TargetLanguage);
            string apiUrl = string.IsNullOrEmpty(config.ApiUrl) ? LibreTranslateConfig.DefaultUrl : config.ApiUrl;

            var requestData = new
            {
                q = text,
                target = targetLanguage,
                source = "auto",
                format = "text",
                api_key = string.IsNullOrEmpty(config.ApiKey) ? null : config.ApiKey
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
                var responseObj = JsonSerializer.Deserialize<LibreTranslateConfig.Response>(responseString);
                return responseObj?.TranslatedText ?? string.Empty;
            }
            throw new Exception($"HTTP Error - {response.StatusCode}");
        }
    }
}
