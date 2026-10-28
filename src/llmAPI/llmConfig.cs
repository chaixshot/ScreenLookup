using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenLookup.src.llmAPI
{
    public class llmConfig : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            App.setting?.Save();
        }
    }

    public class BaseLLMConfig : llmConfig
    {
        public class Message
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
        }

        private string modelName = "";
        private double temperature = 1.0;
        private string translatePrompt = "Trasnlate enclosed in 🔤 to {0}. Remove 🔤 when anwser.";
        private string extraDetailSystemPrompt = "Dictionary assistant. Return JSON only with traditional dictionary respelling (no IPA) and POS-grouped definitions. Never include text in parentheses.";
        private string extraDetailPrompt = "Word/phrase: \"{0}\"\nTarget language: {1}\n\nRules:\n- Respelling: traditional style (e.g. 'ih-THEER-ee-uhl'), NO IPA.\n- Definitions: translated to {1}, clean text only, NO parentheses/notes.\n- Output: JSON only, no markdown.\n\nJSON schema:\n{{\n\"phonetic\": \"string\",\n\"meanings\": [\n{{\n\"pos\": \"string\",\n\"meanings\": [\"string\"]\n}}\n]\n}}";
        public string ModelName
        {
            get => modelName;
            set
            {
                modelName = value;
                OnPropertyChanged("ModelName");
            }
        }

        public double Temperature
        {
            get => temperature;
            set
            {
                temperature = value;
                OnPropertyChanged("Temperature");
            }
        }

        public string TranslatePrompt
        {
            get => translatePrompt;
            set
            {
                translatePrompt = value;
                OnPropertyChanged("Prompt");
            }
        }

        public string ExtraDetailSystemPrompt
        {
            get => extraDetailSystemPrompt;
            set
            {
                extraDetailSystemPrompt = value;
                OnPropertyChanged("SystemPrompt");
            }
        }

        public string ExtraDetailPrompt
        {
            get => extraDetailPrompt;
            set
            {
                extraDetailPrompt = value;
                OnPropertyChanged("UserPrompt");
            }
        }
    }

    public class OllamaConfig : BaseLLMConfig
    {
        public class Response
        {
            public string Model { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public Message Message { get; set; } = new();
            public bool Done { get; set; }
            public long Total_duration { get; set; }
            public int Load_duration { get; set; }
            public int PromptEvalCount { get; set; }
            public long PromptEvalDuration { get; set; }
            public int EvalCount { get; set; }
            public long EvalDuration { get; set; }
        }

        private int timeoutSeconds = 60;
        public static readonly string DefaultUrl = "http://localhost:11434";
        private string apiUrl = DefaultUrl;

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class OpenAIConfig : BaseLLMConfig
    {
        public class Choice
        {
            public int Index { get; set; }
            public Message Message { get; set; } = new();
            public string LogProbs { get; set; } = "";
            public string FinishReason { get; set; } = "";
        }
        public class Usage
        {
            public int PromptTokens { get; set; }
            public int CompletionTokens { get; set; }
            public int TotalTokens { get; set; }
            public int PromptCacheHitTokens { get; set; }
            public int PromptCacheMissTokens { get; set; }
        }
        public class Response
        {
            public string ID { get; set; } = "";
            public string Object { get; set; } = "";
            public int Created { get; set; }
            public string Model { get; set; } = "";
            public List<Choice> Choices { get; set; } = [];
            public Usage Usage { get; set; } = new();
            public string SystemFingerprint { get; set; } = "";
        }

        private int timeoutSeconds = 60;
        private string apiKey = "";
        public static readonly string DefaultUrl = "https://api.openai.com/v1";
        private string apiUrl = DefaultUrl;

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class OpenRouterConfig : BaseLLMConfig
    {
        private int timeoutSeconds = 60;
        private string apiKey = "";
        public static readonly string DefaultUrl = "https://openrouter.ai/api/v1";
        private string apiUrl = DefaultUrl;

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged();
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class LMStudioConfig : BaseLLMConfig
    {
        public class Response
        {
            public string Model { get; set; } = "";
            public string Output { get; set; } = "";
        }

        private int timeoutSeconds = 60;
        public static readonly string DefaultUrl = "http://localhost:1234/api/v1";
        private string apiUrl = DefaultUrl;

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class DeepLConfig : llmConfig
    {
        private int timeoutSeconds = 60;
        private string apiKey = "";
        public static readonly string DefaultUrl = "https://api.deepl.com/v2/translate";
        private string apiUrl = DefaultUrl;

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class YoudaoConfig : llmConfig
    {
        public class TranslationResult
        {
            public string ErrorCode { get; set; } = "";
            public string Query { get; set; } = "";
            public List<string> Translation { get; set; } = [];
            public string L { get; set; } = "";
            public string TSpeakUrl { get; set; } = "";
            public string SpeakUrl { get; set; } = "";
        }

        private int timeoutSeconds = 60;
        private string appKey = "";
        private string appSecret = "";
        public static readonly string DefaultUrl = "https://openapi.youdao.com/api";
        private string apiUrl = DefaultUrl;

        public string AppKey
        {
            get => appKey;
            set
            {
                appKey = value;
                OnPropertyChanged("AppKey");
            }
        }

        public string AppSecret
        {
            get => appSecret;
            set
            {
                appSecret = value;
                OnPropertyChanged("AppSecret");
            }
        }

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class MTranServerConfig : llmConfig
    {
        private int timeoutSeconds = 60;
        private string apiKey = "";
        public static readonly string DefaultUrl = "http://localhost:8989/translate";
        private string apiUrl = DefaultUrl;

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }

        public class Response
        {
            public string Result { get; set; } = "";
        }
    }

    public class BaiduConfig : llmConfig
    {
        public class TransResult
        {
            public string Src { get; set; } = "";
            public string Dst { get; set; } = "";
        }

        public class TranslationResult
        {
            public string ErrorCode { get; set; } = "";
            public string From { get; set; } = "";
            public string To { get; set; } = "";
            public List<TransResult> TransResult { get; set; } = [];
        }

        private int timeoutSeconds = 60;
        private string appId = "";
        private string appSecret = "";
        public static readonly string DefaultUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        private string apiUrl = DefaultUrl;

        public string AppId
        {
            get => appId;
            set
            {
                appId = value;
                OnPropertyChanged("AppId");
            }
        }

        public string AppSecret
        {
            get => appSecret;
            set
            {
                appSecret = value;
                OnPropertyChanged("AppSecret");
            }
        }

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }
    }

    public class LibreTranslateConfig : llmConfig
    {
        private int timeoutSeconds = 60;
        private string apiKey = "";
        public static readonly string DefaultUrl = "http://localhost:5000/translate";
        private string apiUrl = DefaultUrl;

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public int TimeoutSeconds
        {
            get => timeoutSeconds;
            set
            {
                timeoutSeconds = value;
                OnPropertyChanged("TimeoutSeconds");
            }
        }

        public class Response
        {
            public string TranslatedText { get; set; } = "";
        }
    }

    public class ConfigDictConverter : JsonConverter<Dictionary<string, List<llmConfig>>>
    {
        public override Dictionary<string, List<llmConfig>> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected a StartObject token.");
            var configs = new Dictionary<string, List<llmConfig>>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string key = reader.GetString() ?? "";
                reader.Read();

                var configType = Type.GetType($"ScreenLookup.src.models.{key}Config");
                llmConfig config;

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = new List<llmConfig>();
                    reader.Read();

                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (configType != null && typeof(llmConfig).IsAssignableFrom(configType))
                            config = (llmConfig)(JsonSerializer.Deserialize(ref reader, configType, options) ?? new llmConfig());
                        else
                            config = (llmConfig)(JsonSerializer.Deserialize(ref reader, typeof(llmConfig), options) ?? new llmConfig());

                        list.Add(config);
                        reader.Read();
                    }
                    configs[key] = list;
                }
                else
                    throw new JsonException("Expected a StartObject token or a StartArray token.");

                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("Expected an EndObject token.");
            return configs;
        }

        public override void Write(
            Utf8JsonWriter writer, Dictionary<string, List<llmConfig>> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                var configType = Type.GetType($"ScreenLookup.src.models.{kvp.Key}Config");

                if (kvp.Value is IEnumerable<llmConfig> configList)
                {
                    writer.WriteStartArray();
                    foreach (var config in configList)
                    {
                        if (configType != null && typeof(llmConfig).IsAssignableFrom(configType))
                            JsonSerializer.Serialize(writer, config, configType, options);
                        else
                            JsonSerializer.Serialize(writer, config, typeof(llmConfig), options);
                    }
                    writer.WriteEndArray();
                }
                else
                    throw new JsonException($"Unsupported config type: {kvp.Value.GetType()}");
            }
            writer.WriteEndObject();
        }
    }
}
