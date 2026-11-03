using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ScreenLookup.src.models
{
    public class CaptureWordsEntry
    {
        public required string Word { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Padding { get; set; } = "0";
        public double Border { get; set; }
        public int FontSizeS { get; set; }
        public FontFamily FontFace { get; set; } = new("Segoe UI");
        public int SourceLanguage { get; set; }
        public int TargetLanguage { get; set; }
        public int Stop { get; set; } = 0;
        public System.Windows.Visibility Visibility => string.IsNullOrEmpty(Word) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    public class CaptureWordsSimplifiedEntry
    {
        private string _word = string.Empty;
        public required string Word
        {
            get => _word;
            set => _word = value?.Trim().Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
        }
        public required int Stop { get; set; }
    }

    public class CaptureAltoEntry
    {
        public required string Word { get; set; }
        public required string Uid { get; set; }
        public required int SourceLanguage { get; set; }
        public required int TargetLanguage { get; set; }
        public required double X { get; set; }
        public required double Y { get; set; }
        public required double Width { get; set; }
        public required double Height { get; set; }
        public double Angle { get; set; } = 0;
        public int WordOccurrenceIndex { get; set; } = -1;
    }

    public class ImageTranslateEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public required string OriginalText { get; set; }
        private string _translatedText = string.Empty;
        public string TranslatedText
        {
            get => _translatedText;
            set
            {
                _translatedText = value;
                CalculateFontSize();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextVisibility));
            }
        }

        public required double X { get; set; }
        public required double Y { get; set; }
        public required double Width { get; set; }
        public required double Height { get; set; }
        public double Angle { get; set; } = 0;

        private Transform? _transform;
        public Transform Transform
        {
            get => _transform ?? new RotateTransform(Angle);
            set
            {
                _transform = value;
                OnPropertyChanged();
            }
        }

        public required int SourceLanguage { get; set; }
        public required int TargetLanguage { get; set; }

        public double InitialFontSize { get; set; } = 12;

        private double _fontSize = 12;
        public double FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                if (InitialFontSize == 12)
                    InitialFontSize = value;
                OnPropertyChanged();
            }
        }

        public FontFamily FontFace { get; set; } = new("Segoe UI");

        public void CalculateFontSize()
        {
            string text = !string.IsNullOrWhiteSpace(TranslatedText) ? TranslatedText.Trim() : (OriginalText?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(text) || Width <= 0 || Height <= 0)
                return;

            // Exclude Unicode non-spacing marks (combining vowels/tone marks) from horizontal character count
            int effectiveLen = text.Count(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
            if (effectiveLen == 0) effectiveLen = text.Length;

            double usableWidth = Math.Max(1, Width - 6);
            double usableHeight = Math.Max(1, Height - 4);

            // Max font size to fit container height
            double fontSizeByHeight = usableHeight * 0.80;

            // Max font size to fit container width
            double charWidthFactor = 0.58;
            double fontSizeByWidth = usableWidth / (effectiveLen * charWidthFactor);

            // Select the minimum constraint so it fits height without overflowing width, or fits width without overflowing height
            double targetFontSize = Math.Min(fontSizeByHeight, fontSizeByWidth);

            FontSize = Math.Max(6.0, Math.Round(targetFontSize, 1));
        }

        private Brush? _backgroundColor;
        public Brush? BackgroundColor
        {
            get => _backgroundColor ?? (Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush") as Brush ?? Brushes.White);
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCustomColors));
            }
        }

        private Brush? _foregroundColor;
        public Brush? ForegroundColor
        {
            get => _foregroundColor ?? (Application.Current?.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.Black);
            set
            {
                _foregroundColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCustomColors));
            }
        }

        public bool HasCustomColors => _backgroundColor != null && _foregroundColor != null;

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoadingVisibility));
                OnPropertyChanged(nameof(TextVisibility));
            }
        }

        public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TextVisibility => IsLoading ? Visibility.Collapsed : (string.IsNullOrEmpty(TranslatedText) ? Visibility.Collapsed : Visibility.Visible);
    }
}
