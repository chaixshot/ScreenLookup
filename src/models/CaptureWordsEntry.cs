using System.Windows.Media;

namespace ScreenLookup.src.models
{
    public class CaptureWordsEntry
    {
        public required string Word { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        public string Border { get; set; }
        public int FontSizeS { get; set; }
        public FontFamily FontFace { get; set; }
    }

    public class CaptureWordsEntrySimplify
    {
        public required string Word { get; set; }
        public required int Stop { get; set; }
    }
}
