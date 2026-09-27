namespace ScreenLookup.src.models
{
    public class DictionaryEntry
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public int SourceLanguage { get; set; }
        public int TargetLanguage { get; set; }
        public int ProviderServices { get; set; }
        public List<ExtraMeaningEntity>? ExtraMeanings { get; set; }
        public string Phonetic { get; set; } = string.Empty;
    }
}
