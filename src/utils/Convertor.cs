using ScreenLookup.src.models;
using System.Drawing.Drawing2D;
using Bitmap = System.Drawing.Bitmap;
using FontFamily = System.Windows.Media.FontFamily;
using Graphics = System.Drawing.Graphics;

namespace ScreenLookup.src.utils
{
    /// <summary>
    /// Utility class for data conversions and bitmap image transformations (rescaling, rotating).
    /// </summary>
    internal static class Convertor
    {
        #region Model Conversions
        /// <summary>
        /// Converts simplified capture word entries into rich UI-bindable <see cref="CaptureWordsEntry"/> cards with layout attributes.
        /// </summary>
        public static List<CaptureWordsEntry> ConvertCaptureWordsEntry(List<CaptureWordsSimplifiedEntry> data, int sourceLanguage, int targetLanguage, double width = 0)
        {
            List<CaptureWordsEntry> itemsForCard = [];
            bool isFirstLine = true;

            double padding = Math.Max(1.7, App.setting.FontSizeS / 5.5);
            int pendingStop = 0;

            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (item.Stop == 0) // Normal word
                {
                    if (pendingStop > 0 && !isFirstLine)
                    {
                        itemsForCard.Add(new CaptureWordsEntry
                        {
                            Word = string.Empty,
                            Width = 0,
                            Height = 0,
                            Padding = "0",
                            Border = 0,
                            FontSizeS = App.setting.FontSizeS,
                            FontFace = new FontFamily(App.setting.FontFace),
                            SourceLanguage = 0,
                            TargetLanguage = 0,
                            Stop = pendingStop
                        });
                        pendingStop = 0;
                    }

                    isFirstLine = false;
                    itemsForCard.Add(new CaptureWordsEntry
                    {
                        Word = item.Word,
                        Width = double.NaN,
                        Height = double.NaN,
                        Padding = $"{padding}, 0, {padding}, 0",
                        Border = App.setting.ShowHighlight ? 1 : 0,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage,
                        Stop = 0
                    });
                }
                else
                {
                    // It's a stop (1 = new line, 2 = paragraph, 3 = block)
                    if (!isFirstLine)
                    {
                        pendingStop = Math.Max(pendingStop, item.Stop);
                    }
                }
            }

            return itemsForCard;
        }
        #endregion

        #region Bitmap Image Transformations
        /// <summary>
        /// Rescales a bitmap image using high-quality bicubic interpolation.
        /// </summary>
        public static Bitmap BitmapRescale(Bitmap source, double scale)
        {
            int newWidth = Convert.ToInt32(source.Width * scale);
            int newHeight = Convert.ToInt32(source.Height * scale);

            if (newWidth < 1) newWidth = 1;
            if (newHeight < 1) newHeight = 1;

            Bitmap rescaled = new(newWidth, newHeight, source.PixelFormat);
            using Graphics g = Graphics.FromImage(rescaled);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, newWidth, newHeight);

            return rescaled;
        }

        /// <summary>
        /// Rotates a bitmap image by the specified angle in degrees around its center, automatically computing new bounds.
        /// </summary>
        public static Bitmap BitmapRotate(Bitmap source, float angle)
        {
            angle %= 360;
            if (angle > 180)
                angle -= 360;

            float sin = MathF.Abs(MathF.Sin(angle * MathF.PI / 180.0f));
            float cos = MathF.Abs(MathF.Cos(angle * MathF.PI / 180.0f));
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

            Bitmap rotated = new((int)newImgWidth, (int)newImgHeight, source.PixelFormat);
            using Graphics g = Graphics.FromImage(rotated);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(originX, originY);
            g.RotateTransform(angle);
            g.DrawImageUnscaled(source, 0, 0);

            return rotated;
        }
        #endregion
    }
}
