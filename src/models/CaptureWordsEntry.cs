using System.Windows.Media;

namespace ScreenLookup.src.models
{
    public class CaptureWordsEntry
    {
        public required string Word { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Border { get; set; }
        public int FontSizeS { get; set; }
        public FontFamily FontFace { get; set; }
        public int SourceLanguage { get; set; }
        public int TargetLanguage { get; set; }
    }

    public class CaptureWordsEntrySimplify
    {
        public required string Word { get; set; }
        public required int Stop { get; set; }
    }
}
