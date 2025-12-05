using Microsoft.Win32;
using ScreenLookup.src.models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    public partial class SettingPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public SettingPage()
        {
            DataContext = this;
            InitializeComponent();

            SourceLanguge = Setting.RegSetting.GetValue("SourceLanguge") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("SourceLanguge")) : 0;
            TargetLanguge = Setting.RegSetting.GetValue("TargetLanguge") != null ? Convert.ToInt32(Setting.RegSetting.GetValue("TargetLanguge")) : 1;
            StartupWithWindows = Setting.RegSetting.GetValue("StartupWithWindows") == null || Setting.RegSetting.GetValue("StartupWithWindows").ToString() == "True";
            StartInBackground = Setting.RegSetting.GetValue("StartInBackground") != null && Setting.RegSetting.GetValue("StartInBackground").ToString() == "True";
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

        public async Task CopyFileWithElevatedPermissions(string sourcePath, string destinationPath)
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
            string? pickedLanguageFile = LangugeList.LanguageTesseract[sourceLanguge.SelectedIndex]+ ".traineddata";
            if (string.IsNullOrWhiteSpace(pickedLanguageFile))
                return;

            string tesseractFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}tessdata";
            string tempFilePath = Path.Combine(Path.GetTempPath(), pickedLanguageFile);

            TesseractGitHubFileDownloader fileDownloader = new();
            await fileDownloader.DownloadFileAsync(pickedLanguageFile, tempFilePath);
            await CopyFileWithElevatedPermissions(tempFilePath, tesseractFilePath);
            File.Delete(tempFilePath);
        }
    }
}
