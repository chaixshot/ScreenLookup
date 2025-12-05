using GTranslate;
using Microsoft.Win32;
using ScreenLookup.src.models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScreenLookup.src.pages
{
    public partial class SettingPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool isLoading = false;

        public SettingPage()
        {
            DataContext = this;
            InitializeComponent();

            SourceLanguage = Setting.RegSetting.GetValue("SourceLanguage") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("SourceLanguage")) : 29;
            TargetLanguage = Setting.RegSetting.GetValue("TargetLanguage") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("TargetLanguage")) : 117;
            StartupWithWindows = Setting.RegSetting.GetValue("StartupWithWindows") == null || Setting.RegSetting.GetValue("StartupWithWindows").ToString() == "True";
            StartInBackground = Setting.RegSetting.GetValue("StartInBackground") != null && Setting.RegSetting.GetValue("StartInBackground").ToString() == "True";
            MinimizeToTray = Setting.RegSetting.GetValue("MinimizeToTray") == null || Setting.RegSetting.GetValue("MinimizeToTray").ToString() == "True";

            LoadTesseractContent();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int SourceLanguage
        {
            get { return Setting.SourceLanguage; }
            set
            {
                Setting.SourceLanguage = value;
                ToggleDownloadTesseractButton();
                OnPropertyChanged();
            }
        }

        public int TargetLanguage
        {
            get { return Setting.TargetLanguage; }
            set
            {
                Setting.TargetLanguage = value;
                OnPropertyChanged();
            }
        }

        public bool StartupWithWindows
        {
            get { return Setting.StartupWithWindows; }
            set
            {
                Setting.StartupWithWindows = value;

                if (value)
                    Setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
                else
                    Setting.RegAutorun.DeleteValue("ScreenLookup", false);

                OnPropertyChanged();
            }
        }

        public bool StartInBackground
        {
            get { return Setting.StartInBackground; }
            set
            {
                Setting.StartInBackground = value;
                OnPropertyChanged();
            }
        }
        public bool MinimizeToTray
        {
            get { return Setting.MinimizeToTray; }
            set
            {
                Setting.MinimizeToTray = value;
                OnPropertyChanged();
            }
        }

        private void LoadTesseractContent()
        {
            sourceLanguage.Items.Clear();
            targetLanguage.Items.Clear();

            foreach (string textName in LanguageList.LanguageTesseract)
            {
                string tesseractTag = LanguageList.GetTesseractTagFromName(textName);

                string text = $"{LanguageList.CultureDisplayName(tesseractTag).PadRight(42)}\t{textName}";
                sourceLanguage.Items.Add(text);
                targetLanguage.Items.Add(text);
            }
        }

        private void ToggleDownloadTesseractButton()
        {
            if (isLoading || Setting.IsLanguageInstalled(Setting.SourceLanguage))
                downloadTesseract.Visibility = Visibility.Hidden;
            else
                downloadTesseract.Visibility = Visibility.Visible;
        }

        private static async Task CopyFileWithElevatedPermissions(string sourcePath, string destinationPath)
        {
            // Create tessdata folder
            string arguments = $"/c if not exist \"{destinationPath}\" mkdir \"{destinationPath}\"";
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "cmd.exe",
                Verb = "runas",
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                Process? process = Process.Start(startInfo);
                if (process is not null)
                    await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            // Move *.traineddata
            arguments = $"/c copy \"{sourcePath}\" \"{destinationPath}\"";
            startInfo = new()
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "cmd.exe",
                Verb = "runas",
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                Process? process = Process.Start(startInfo);
                if (process is not null)
                    await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;

            string? pickedLanguageFile = LanguageList.LanguageTesseract[langID];
            if (isLoading || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isLoading = true;
            Notification.Show($"Downloading {LanguageList.CultureDisplayName(LanguageList.GetTesseractTagFromID(langID))}", 500);
            downloadTesseract.Visibility = Visibility.Hidden;

            string tesseractFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}tessdata";
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractGitHubFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await CopyFileWithElevatedPermissions(tempFilePath, tesseractFilePath);
            File.Delete(tempFilePath);

            isLoading = false;
            Setting.RegDownloadedLang.SetValue(LanguageList.GetTesseractTagFromID(langID), true);
            ToggleDownloadTesseractButton();
            Notification.Show($"Download {LanguageList.CultureDisplayName(LanguageList.GetTesseractTagFromID(langID))} successfully", 500);
        }
    }
}
