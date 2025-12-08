using ScreenLookup.src.models;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    class Convertor
    {
        public static MainWindow mainWindow = (App.Current.MainWindow as MainWindow);
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsEntrySimplify> data)
        {
            List<CaptureWordsEntry> itemsForCard = [];

            foreach (var item in data)
            {
                if (item.Stop == 0)
                    itemsForCard.Add(new CaptureWordsEntry() { Word = item.Word, Border = "1", FontSizeS = Setting.FontSizeS, FontFace = new FontFamily(Setting.FontFace) });
                if (item.Stop == 1)
                    itemsForCard.Add(new CaptureWordsEntry() { Word = "", Width = mainWindow.Width.ToString(), Height = "0" });
                if (item.Stop == 2)
                    itemsForCard.Add(new CaptureWordsEntry() { Word = "", Width = mainWindow.Width.ToString() });
                if (item.Stop == 3)
                    itemsForCard.Add(new CaptureWordsEntry() { Word = "", Width = mainWindow.Width.ToString() });
            }

            return itemsForCard;
        }
    }
}
