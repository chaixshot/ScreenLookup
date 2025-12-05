using Microsoft.Win32;
using ScreenLookup.src.models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    }
}
