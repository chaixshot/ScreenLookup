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

        public static int sourceLanguge;
        public static int targetLanguge;
        public static bool startupWithWindows;
        public static bool startInBackground;

        public static int SourceLanguge
        {
            get { return sourceLanguge; }
            set
            {
                sourceLanguge = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("SourceLanguge", value.ToString());
            }
        }

        public static int TargetLanguge
        {
            get { return targetLanguge; }
            set
            {
                targetLanguge = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TargetLanguge", value.ToString());
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
    }
}
