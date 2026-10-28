using System.Collections.Specialized;

namespace ScreenLookup.src.llmAPI
{
    public class BaseLLMRequestData
    {
        public string Model { get; set; }
        public List<BaseLLMConfig.Message> Messages { get; set; }
        public double Temperature { get; set; }

        public BaseLLMRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
        {
            this.Model = model;
            this.Messages = messages;
            this.Temperature = temperature;
        }
    }

    public class OllamaRequestData : BaseLLMRequestData
    {
        public OllamaRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature) { }
    }

    public class OpenRouterRequestData : BaseLLMRequestData
    {
        public OpenRouterRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature) { }
    }

    public class OpenAIRequestData : BaseLLMRequestData
    {
        public OpenAIRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature) { }
    }

    public static class llmRequestDataFactory
    {
        private static readonly OrderedDictionary typeSequence = new()
        {
            ["Ollama"] = typeof(OllamaRequestData),
            ["OpenRouter"] = typeof(OpenRouterRequestData),
            ["OpenAI"] = typeof(OpenAIRequestData),
            ["base"] = typeof(BaseLLMRequestData)
        };

        public static int FallbackCount => typeSequence.Count;

        public static BaseLLMRequestData Create(string platform, string model, List<BaseLLMConfig.Message> messages, double temperature)
        {
            if (typeSequence[platform] == null)
                return new BaseLLMRequestData(model, messages, temperature);
            return (BaseLLMRequestData)Activator.CreateInstance((Type)typeSequence[platform]!, model, messages, temperature)!;
        }

        public static BaseLLMRequestData Create(int index, string model, List<BaseLLMConfig.Message> messages, double temperature)
        {
            if (typeSequence[index] == null)
                return new BaseLLMRequestData(model, messages, temperature);
            return (BaseLLMRequestData)Activator.CreateInstance((Type)typeSequence[index]!, model, messages, temperature)!;
        }
    }
}
