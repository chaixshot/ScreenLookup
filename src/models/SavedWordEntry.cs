using System.Windows;

namespace ScreenLookup.src.models
{
    public class SavedWordEntry
    {
        public required string Id { get; set; }
        public required string Original { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
        public Visibility ScoreVisibility { get; set; }
    }
}
