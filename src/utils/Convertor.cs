using ScreenLookup.src.models;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    class Convertor
    {
        public static MainWindow mainWindow = (App.Current.MainWindow as MainWindow);
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsEntrySimplify> data, int sourceLanguage = 0, int targetLanguage = 0)
        {
            List<CaptureWordsEntry> itemsForCard = [];

            foreach (var item in data)
            {
                if (item.Stop == 0)
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = item.Word,
                        Width = Double.NaN,
                        Height = Double.NaN,
                        Border = 1,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage
                    });
                if (item.Stop == 1)
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = mainWindow.Width,
                        Height = 0,
                        Border = 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 2)
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = mainWindow.Width,
                        Height = Double.NaN,
                        Border = 0,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                        SourceLanguage = 0,
                        TargetLanguage = 0
                    });
                if (item.Stop == 3)
                    itemsForCard.Add(new CaptureWordsEntry()
                    {
                        Word = "",
                        Width = mainWindow.Width,
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
