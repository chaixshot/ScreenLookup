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

            SourceLanguge = Setting.RegSetting.GetValue("SourceLanguge") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("SourceLanguge")) : 0;
            TargetLanguge = Setting.RegSetting.GetValue("TargetLanguge") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("TargetLanguge")) : 1;
            StartupWithWindows = Setting.RegSetting.GetValue("StartupWithWindows") == null || Setting.RegSetting.GetValue("StartupWithWindows").ToString() == "True";
            StartInBackground = Setting.RegSetting.GetValue("StartInBackground") != null && Setting.RegSetting.GetValue("StartInBackground").ToString() == "True";

            LoadTesseractContent();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int SourceLanguge
        {
            get { return Setting.SourceLanguge; }
            set
            {
                Setting.SourceLanguge = value;
                ToggleDownloadTesseractButton();
                OnPropertyChanged();
            }
        }

        public int TargetLanguge
        {
            get { return Setting.TargetLanguge; }
            set
            {
                Setting.TargetLanguge = value;
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

        private void LoadTesseractContent()
        {
            sourceLanguge.Items.Clear();
            targetLanguge.Items.Clear();

            foreach (string textName in LangugeList.LanguageTesseract)
            {
                string tesseractTag = LangugeList.GetTesseractTagFromName(textName);

                string text = $"{LangugeList.CultureDisplayName(tesseractTag).PadRight(42)}\t{textName}";
                sourceLanguge.Items.Add(text);
                targetLanguge.Items.Add(text);
            }
        }

        private void ToggleDownloadTesseractButton()
        {
            var lang = Setting.RegDownloadedLang.GetValue(LangugeList.GetTesseractTagFromID(Setting.SourceLanguge));
            if (lang == null)
                downloadTesseract.Visibility = Visibility.Visible;
            else
                downloadTesseract.Visibility = Visibility.Hidden;
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
            string? pickedLanguageFile = LangugeList.LanguageTesseract[sourceLanguge.SelectedIndex];
            if (isLoading || string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            isLoading = true;

            string tesseractFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}tessdata";
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractGitHubFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await CopyFileWithElevatedPermissions(tempFilePath, tesseractFilePath);
            File.Delete(tempFilePath);

            Setting.RegDownloadedLang.SetValue(LangugeList.GetTesseractTagFromID(sourceLanguge.SelectedIndex), true);
            ToggleDownloadTesseractButton();

            isLoading = false;
        }
    }
}
