using ScreenLookup.src.models;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    class Convertor
    {
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsEntrySimplify> data, int sourceLanguage = 0, int targetLanguage = 0, double width = 200)
        {
            List<CaptureWordsEntry> itemsForCard = [];

            double padding = Math.Max(1.7, App.setting.FontSizeS / 5.5);
            foreach (var item in data)
            {
                if (item.Stop == 0) // Normal
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = item.Word,
                        Width = Double.NaN,
                        Height = Double.NaN,
                        Padding = $"{padding}, 0, {padding}, 0",
                        Border = App.setting.ShowHighlight ? 1 : 0,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage
                    });
                if (item.Stop == 1) // New line
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = 0,
                        Padding = "0",
                        Border = 0,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 2) // New paragraph
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = Double.NaN,
                        Padding = "0",
                        Border = 0,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 3) // New block
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = width,
                        Height = Double.NaN,
                        Padding = "0",
                        Border = 0,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
            }

            return itemsForCard;
        }
    }
}
