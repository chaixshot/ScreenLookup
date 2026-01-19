using ScreenLookup.src.models;
using System.Drawing.Drawing2D;
using Bitmap = System.Drawing.Bitmap;
using FontFamily = System.Windows.Media.FontFamily;
using Graphics = System.Drawing.Graphics;


namespace ScreenLookup.src.utils
{
    class Convertor
    {
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsSimplifiedEntry> data, int sourceLanguage, int targetLanguage, double width)
        {
            List<CaptureWordsEntry> itemsForCard = [];
            bool isFirstLine = true;

            double padding = Math.Max(1.7, App.setting.FontSizeS / 5.5);
            foreach (var item in data)
            {
                if (item.Stop == 0)// Normal
                {
                    isFirstLine = false;
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
                }

                if (!isFirstLine)
                {
                    if (item.Stop == 1) // New line
                        itemsForCard.Add(new CaptureWordsEntry()
                        {
                            Word = string.Empty,
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
                            Word = string.Empty,
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
                            Word = string.Empty,
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
            }

            return itemsForCard;
        }

        public static Bitmap BitmapRescale(Bitmap source, double scale)
        {

            Bitmap rescaled = new(Convert.ToInt32(source.Width * scale), Convert.ToInt32(source.Height * scale), source.PixelFormat);
            Graphics g = Graphics.FromImage(rescaled);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, Convert.ToInt32(source.Width * scale), Convert.ToInt32(source.Height * scale));

            return rescaled;
        }

        public static Bitmap BitmapRotate(Bitmap source, float angle)
        {
            angle %= 360;
            if (angle > 180)
                angle -= 360;

            float sin = (float)Math.Abs(Math.Sin(angle * Math.PI / 180.0)); // this function takes radians
            float cos = (float)Math.Abs(Math.Cos(angle * Math.PI / 180.0)); // this one too
            float newImgWidth = sin * source.Height + cos * source.Width;
            float newImgHeight = sin * source.Width + cos * source.Height;
            float originX = 0f;
            float originY = 0f;

            if (angle > 0)
            {
                if (angle <= 90)
                    originX = sin * source.Height;
                else
                {
                    originX = newImgWidth;
                    originY = newImgHeight - sin * source.Width;
                }
            }
            else
            {
                if (angle >= -90)
                    originY = sin * source.Width;
                else
                {
                    originX = newImgWidth - sin * source.Height;
                    originY = newImgHeight;
                }
            }

            Bitmap rotated = new Bitmap((int)newImgWidth, (int)newImgHeight, source.PixelFormat);
            Graphics g = Graphics.FromImage(rotated);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(originX, originY); // offset the origin to our calculated values
            g.RotateTransform(angle); // set up rotate
            g.DrawImageUnscaled(source, 0, 0); // draw the image at 0, 0
            g.Dispose();

            return rotated;
        }
    }
}
