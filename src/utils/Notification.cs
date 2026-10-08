using Microsoft.Toolkit.Uwp.Notifications;
using System.Text;

namespace ScreenLookup.src.utils
{
    /// <summary>
    /// Utility class for displaying Windows toast notifications.
    /// </summary>
    public static class Notification
    {
        public static void Show(string copiedText = "Notification")
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(copiedText);

            // Base64 encode string for toast argument (strip trailing '=')
            string encodedString = Convert.ToBase64String(plainTextBytes).TrimEnd('=');

            // Truncate toast body text if too long
            string toastBody = copiedText.Length > 150 ? copiedText[..150] + "..." : copiedText;

            // Build the toast XML
            ToastContentBuilder toast = new ToastContentBuilder()
                .AddArgument("text", encodedString)
                .AddText("ScreenLookup")
                .AddText(toastBody);

            int toastSizeInBytes = Encoding.UTF8.GetByteCount(toast.Content.GetContent());
            if (toastSizeInBytes > 5000) // Maximum toast size is 5000 bytes
            {
                int bytesFree = 5000 - (toastSizeInBytes - encodedString.Length);
                int maxTextBytes = bytesFree / 4 * 3;

                if (bytesFree % 4 >= 2)
                    maxTextBytes += bytesFree % 4 - 1;

                plainTextBytes = new byte[maxTextBytes];
                Encoding.UTF8.GetEncoder().Convert(copiedText.AsSpan(), plainTextBytes.AsSpan(), true, out _, out int bytesUsed, out _);

                encodedString = Convert.ToBase64String(plainTextBytes, 0, bytesUsed).TrimEnd('=');

                toast = new ToastContentBuilder()
                    .AddArgument("text", encodedString)
                    .AddText("ScreenLookup")
                    .AddText(toastBody);
            }

            toast.Show();
        }
    }
}
