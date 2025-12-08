using System.Windows.Media;

namespace ScreenLookup.src.models
{
    public class HistoryLoggerEntry
    {
        public required string Id { get; set; }
        public required string Original { get; set; }
        public required string OriginalWords { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
    }

    public class HistoryLoggerExportEntry
    {
        public required string Original { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
    }

    public class HistoryLoggerPageEntry
    {
        public required string Id { get; set; }
        public required string Original { get; set; }
        public required List<CaptureWordsEntry> OriginalWords { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
        public required int FontSizeS { get; set; }
        public required FontFamily FontFace { get; set; }
    }
}
