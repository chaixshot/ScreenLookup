using ScreenLookup.src.models;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    class Convertor
    {
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsEntrySimplify> data, int sourceLanguage = 0, int targetLanguage = 0, double width = 200)
        {
            List<CaptureWordsEntry> itemsForCard = [];

            foreach (var item in data)
            {
                if (item.Stop == 0) // Normal
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = item.Word,
                        Width = Double.NaN,
                        Height = Double.NaN,
                        Border = Setting.ShowHighlight ? 1 : 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage
                    });
                if (item.Stop == 1) // New line
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = 0,
                        Border = 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 2) // New paragraph
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = Double.NaN,
                        Border = 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 3) // New block
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = Double.NaN,
                        Border = 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
            }

            return itemsForCard;
        }
    }
}
