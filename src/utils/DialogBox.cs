using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    class DialogBox
    {
        public static async Task<bool> Show(string title, string content, int type)
        {
            var dialogHostContainer = (App.Current.MainWindow as MainWindow)?.DialogHostContainer;
            var dialog = new ContentDialog
            {
                Title = new Wpf.Ui.Controls.TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeights.Regular
                },
                Content = content,
                PrimaryButtonText = type == 0 ? "Yes" : "",
                CloseButtonText = type == 0 ? "No" : "OK",
                DefaultButton = ContentDialogButton.Close,
                DialogHost = dialogHostContainer,
                Padding = new Thickness(8, 4, 8, 8),
            };

            dialogHostContainer.Visibility = Visibility.Visible;
            var result = await dialog.ShowAsync();
            dialogHostContainer.Visibility = Visibility.Collapsed;

            return result == ContentDialogResult.Primary;
        }
    }
}
