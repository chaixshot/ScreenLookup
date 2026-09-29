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

    public class ExtraMeaningEntity
    {
        private string _pos = string.Empty;
        public string Pos
        {
            get => _pos;
            set => _pos = value?.Trim().Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
        }

        private List<string> _meanings = [];
        public List<string> Meanings
        {
            get => _meanings;
            set => _meanings = value?
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim().Normalize(System.Text.NormalizationForm.FormC))
                .ToList() ?? [];
        }

        public string MeaningsFormatted => string.Join(", ", Meanings);
    }
}
