using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace ScreenLookup.src.models
{
    internal class Setting
    {
        public static readonly RegistryKey RegSetting = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
        public static readonly RegistryKey RegWindowBounds = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\WindowBounds\\");
        public static readonly RegistryKey RegDownloadedLang = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\DownloadedLang\\");
        public static readonly RegistryKey RegAutorun = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

        public static int sourceLanguage;
        public static int targetLanguage;
        public static bool startupWithWindows = true;
        public static bool startInBackground = true;
        public static bool minimizeToTray = true;
        public static bool topmost = false;

        public static int SourceLanguage
        {
            get { return sourceLanguage; }
            set
            {
                sourceLanguage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("SourceLanguage", value.ToString());
            }
        }

        public static int TargetLanguage
        {
            get { return targetLanguage; }
            set
            {
                targetLanguage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TargetLanguage", value.ToString());
            }
        }

        public static bool StartupWithWindows
        {
            get { return startupWithWindows; }
            set
            {
                startupWithWindows = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("StartupWithWindows", value.ToString());
            }
        }

        public static bool StartInBackground
        {
            get { return startInBackground; }
            set
            {
                startInBackground = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("StartInBackground", value.ToString());
            }
        }

        public static bool MinimizeToTray
        {
            get { return minimizeToTray; }
            set
            {
                minimizeToTray = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("MinimizeToTray", value.ToString());
            }
        }

        public static bool Topmost
        {
            get { return topmost; }
            set
            {
                topmost = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("Topmost", value.ToString());
            }
        }

        public static bool IsLanguageInstalled(int langID)
        {
            var reg = RegDownloadedLang.GetValue(LanguageList.GetTesseractTagFromID(langID));

            return reg != null;
        }
    }
}
