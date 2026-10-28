using System.Text.Json;

namespace ScreenLookup.src.llmAPI
{
    public static class llmModelsService
    {
        private static readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            return url.Trim().TrimEnd('/');
        }

        public static string GetModelsEndpoint(string apiName, string baseUrl)
        {
            return apiName switch
            {
                "LMStudio" => NormalizeUrl(baseUrl) + "/models",
                "Ollama" => NormalizeUrl(baseUrl) + "/api/tags",
                _ => string.Empty
            };
        }

        public static async Task<List<ModelInfo>> FetchModelsAsync(string apiName, string baseUrl, CancellationToken token = default)
        {
            string endpoint = GetModelsEndpoint(apiName, baseUrl);
            if (string.IsNullOrEmpty(endpoint))
                return [];

            try
            {
                var response = await client.GetAsync(endpoint, token);
                if (!response.IsSuccessStatusCode)
                    return [];

                string json = await response.Content.ReadAsStringAsync(token);

                return apiName switch
                {
                    "LMStudio" => ParseLMStudioModels(json),
                    "Ollama" => ParseOllamaModels(json),
                    _ => []
                };
            }
            catch
            {
                return [];
            }
        }

        public class ModelInfo
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }

        private static List<ModelInfo> ParseLMStudioModels(string json)
        {
            var result = new List<ModelInfo>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("models", out var modelsArray))
                    return result;

                foreach (var model in modelsArray.EnumerateArray())
                {
                    string? type = model.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                    if (type != "llm" && type != null)
                        continue;

                    string? key = model.TryGetProperty("id", out var idProp) ? idProp.GetString() : (model.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    string displayName = model.TryGetProperty("display_name", out var dnProp) ? dnProp.GetString() : key;

                    result.Add(new ModelInfo { Id = key, DisplayName = displayName ?? key });
                }
            }
            catch { }

            return result;
        }

        private static List<ModelInfo> ParseOllamaModels(string json)
        {
            var result = new List<ModelInfo>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("models", out var modelsArray))
                    return result;

                foreach (var model in modelsArray.EnumerateArray())
                {
                    string? name = model.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    result.Add(new ModelInfo { Id = name, DisplayName = name });
                }
            }
            catch { }

            return result;
        }
    }
}
