using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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

    public class HistoryLoggerPageEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _id = string.Empty;
        private string _original = string.Empty;
        private List<CaptureWordsEntry> _originalWords = [];
        private Visibility _reTranslate = Visibility.Collapsed;
        private string _translated = string.Empty;
        private string _sourceLanguage = string.Empty;
        private string _targetLanguage = string.Empty;
        private int _fontSizeS;
        private FontFamily _fontFace = new();

        public required string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public required string Original
        {
            get => _original;
            set { _original = value; OnPropertyChanged(); }
        }

        public required List<CaptureWordsEntry> OriginalWords
        {
            get => _originalWords;
            set { _originalWords = value; OnPropertyChanged(); }
        }

        public required Visibility ReTranslate
        {
            get => _reTranslate;
            set { _reTranslate = value; OnPropertyChanged(); }
        }

        public required string Translated
        {
            get => _translated;
            set { _translated = value; OnPropertyChanged(); }
        }

        public required string SourceLanguage
        {
            get => _sourceLanguage;
            set { _sourceLanguage = value; OnPropertyChanged(); }
        }

        public required string TargetLanguage
        {
            get => _targetLanguage;
            set { _targetLanguage = value; OnPropertyChanged(); }
        }

        public required int FontSizeS
        {
            get => _fontSizeS;
            set { _fontSizeS = value; OnPropertyChanged(); }
        }

        public required FontFamily FontFace
        {
            get => _fontFace;
            set { _fontFace = value; OnPropertyChanged(); }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
