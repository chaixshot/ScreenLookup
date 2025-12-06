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
using Wpf.Ui.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScreenLookup.src.pages
{
    public partial class SettingPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool isDownloading = false;

        public SettingPage()
        {
            DataContext = this;
            InitializeComponent();

            LoadSetting();
            LoadTesseractContent();
            LoadTranslationProvidersContent();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int SourceLanguageAccuracy
        {
            get { return Setting.SourceLanguageAccuracy; }
            set
            {
                Setting.SourceLanguageAccuracy = value;
                DownloadTesseractButtonStateChange();
                OnPropertyChanged();
            }
        }

        public int SourceLanguage
        {
            get { return Setting.SourceLanguage; }
            set
            {
                Setting.SourceLanguage = value;
                DownloadTesseractButtonStateChange();
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

        public int TranslationProvider
        {
            get { return Setting.TranslationProvider; }
            set
            {
                Setting.TranslationProvider = value;
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
        public bool Topmost
        {
            get { return Setting.Topmost; }
            set
            {
                Setting.Topmost = value;
                OnPropertyChanged();
            }
        }

        private void LoadSetting()
        {
            SourceLanguageAccuracy = Setting.RegSetting.GetValue("SourceLanguageAccuracy") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("SourceLanguageAccuracy")) : 1;
            SourceLanguage = Setting.RegSetting.GetValue("SourceLanguage") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("SourceLanguage")) : 29;
            TargetLanguage = Setting.RegSetting.GetValue("TargetLanguage") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("TargetLanguage")) : 117;
            TranslationProvider = Setting.RegSetting.GetValue("TranslationProvider") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("TranslationProvider")) : 1;
            StartupWithWindows = Setting.RegSetting.GetValue("StartupWithWindows") == null || Setting.RegSetting.GetValue("StartupWithWindows").ToString() == "True";
            StartInBackground = Setting.RegSetting.GetValue("StartInBackground") != null && Setting.RegSetting.GetValue("StartInBackground").ToString() == "True";
            MinimizeToTray = Setting.RegSetting.GetValue("MinimizeToTray") == null || Setting.RegSetting.GetValue("MinimizeToTray").ToString() == "True";
            Topmost = Setting.RegSetting.GetValue("Topmost") != null && Setting.RegSetting.GetValue("Topmost").ToString() == "True";
        }
        private void LoadTesseractContent()
        {
            sourceLanguage.Items.Clear();
            targetLanguage.Items.Clear();

            foreach (string languageTesseract in LanguageList.LanguageTesseract)
            {
                string tesseractTag = LanguageList.GetTesseractTagFromLanguageTesseract(languageTesseract);

                string text = $"{LanguageList.CultureDisplayNameFromTesseractTag(tesseractTag).PadRight(42)}\t{languageTesseract}";
                sourceLanguage.Items.Add(text);
                targetLanguage.Items.Add(text);
            }
        }

        private void LoadTranslationProvidersContent()
        {
            translationProvider.Items.Clear();

            foreach (string translationProvider in Setting.TranslationProviders)
            {
                this.translationProvider.Items.Add(translationProvider);
            }
        }

        private void DownloadTesseractButtonStateChange()
        {
            var symbolIcon = downloadTesseract?.Icon as SymbolIcon;

            symbolIcon.Filled = false;
            if (isDownloading)
                symbolIcon.Symbol = SymbolRegular.ClockArrowDownload24;
            else if (Setting.IsLanguageInstalled(Setting.SourceLanguageAccuracy, Setting.SourceLanguage))
                downloadTesseract.Visibility = Visibility.Hidden;
            else
            {
                symbolIcon.Symbol = SymbolRegular.ArrowDownload24;
                downloadTesseract.Visibility = Visibility.Visible;
            }
        }

        private static async Task MoveFileToFolder(string sourcePath, string destinationPath)
        {
            System.IO.Directory.CreateDirectory(destinationPath);
            FileInfo file = new FileInfo(sourcePath);
            file.MoveTo($@"{destinationPath}\{file.Name}");
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            int langID = Setting.SourceLanguage;
            int accID = Setting.SourceLanguageAccuracy;

            string? pickedLanguageFile = LanguageList.LanguageTesseract[langID];
            if (isDownloading || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isDownloading = true;
            DownloadTesseractButtonStateChange();
            Notification.Show($"Downloading {LanguageList.CultureDisplayNameFromID(langID)}", 500);

            string tesseractFilePath = TesseractHelper.GetTessdataPath();
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractGitHubFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await MoveFileToFolder(tempFilePath, tesseractFilePath);

            isDownloading = false;
            Setting.SaveLanguageInstalled(accID, langID);
            DownloadTesseractButtonStateChange();
            Notification.Show($"Download {LanguageList.CultureDisplayNameFromID(langID)} successfully", 500);
        }
    }
}
