using Microsoft.Win32;
using ScreenLookup.src.models;
using System.Text.Json;
using System.Windows.Input;

namespace ScreenLookup.src.utils
{
    internal class Setting
    {
        public static MainWindow? mainWindow = (App.Current.MainWindow as MainWindow);

        public static RegistryKey ScreenLookupReg = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup");
        public static readonly RegistryKey RegSetting = ScreenLookupReg.CreateSubKey("Settings");
        public static readonly RegistryKey RegWindowBounds = ScreenLookupReg.CreateSubKey("WindowBounds");
        public static readonly RegistryKey RegLoadedTesseract = ScreenLookupReg.CreateSubKey("InstalledTesseract");
        public static readonly RegistryKey RegLoadedHunspell = ScreenLookupReg.CreateSubKey("InstalledHunspell");
        public static readonly RegistryKey RegAutorun = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

        public static int sourceLanguageAccuracy = RegSetting.GetValue("SourceLanguageAccuracy") != null ? Convert.ToInt32(RegSetting.GetValue("SourceLanguageAccuracy")) : 1;
        public static int sourceLanguage = RegSetting.GetValue("SourceLanguage") != null ? Convert.ToInt32(RegSetting.GetValue("SourceLanguage")) : 29;
        public static bool hunSpell = RegSetting.GetValue("hunSpell") != null && RegSetting.GetValue("hunSpell").ToString() == "True";
        public static int targetLanguage = RegSetting.GetValue("TargetLanguage") != null ? Convert.ToInt32(RegSetting.GetValue("TargetLanguage")) : 117;
        public static int translationProvider = RegSetting.GetValue("TranslationProvider") != null ? Convert.ToInt32(RegSetting.GetValue("TranslationProvider")) : 1;
        public static int ttsProvider = RegSetting.GetValue("TTSProvider") != null ? Convert.ToInt32(RegSetting.GetValue("TTSProvider")) : 1;
        public static ShortcutKeySet shortcutKey = RegSetting.GetValue("ShortcutKey") != null ? JsonSerializer.Deserialize<ShortcutKeySet>(RegSetting.GetValue("ShortcutKey").ToString()) : new ShortcutKeySet()
        {
            Modifiers = { ModifierKeys.Alt },
            NonModifierKey = Key.Z,
        };
        public static bool startupWithWindows = RegSetting.GetValue("StartupWithWindows") == null || RegSetting.GetValue("StartupWithWindows").ToString() == "True";
        public static bool startInBackground = RegSetting.GetValue("StartInBackground") != null && RegSetting.GetValue("StartInBackground").ToString() == "True";
        public static bool minimizeToTray = RegSetting.GetValue("MinimizeToTray") == null || RegSetting.GetValue("MinimizeToTray").ToString() == "True";
        public static bool showImage = RegSetting.GetValue("ShowImage") == null || RegSetting.GetValue("ShowImage").ToString() == "True";
        public static bool closeLostFocus = RegSetting.GetValue("CloseLostFocus") == null || RegSetting.GetValue("CloseLostFocus").ToString() == "True";
        public static bool topmost = RegSetting.GetValue("Topmost") == null || RegSetting.GetValue("Topmost").ToString() == "True";
        public static int fontSizes = RegSetting.GetValue("FontSizeS") != null ? Convert.ToInt32(RegSetting.GetValue("FontSizeS")) : 14;
        public static string fontFace = RegSetting.GetValue("FontFace") != null ? RegSetting.GetValue("FontFace").ToString() : "Segoe UI";

        public static readonly string[] ProviderServices = [
            "Google",
            "Google New",
            "Bing",
            "Microsoft Azure",
            "Yandex",
        ];

        public static readonly string[] SourceAccuracys = [
            "Fast (Bad)",
            "Normal",
            "Slow (Accurate)",
        ];

        public static int SourceLanguageAccuracy
        {
            get { return sourceLanguageAccuracy; }
            set
            {
                sourceLanguageAccuracy = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("SourceLanguageAccuracy", value.ToString());
            }
        }

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

        public static bool HunSpell
        {
            get { return hunSpell; }
            set
            {
                hunSpell = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("HunSpell", value.ToString());
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

        public static int TranslationProvider
        {
            get { return translationProvider; }
            set
            {
                translationProvider = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TranslationProvider", value.ToString());
            }
        }
        public static int TTSProvider
        {
            get { return ttsProvider; }
            set
            {
                ttsProvider = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TTSProvider", value.ToString());
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

        public static bool ShowImage
        {
            get { return showImage; }
            set
            {
                showImage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("ShowImage", value.ToString());
            }
        }
        public static bool CloseLostFocus
        {
            get { return closeLostFocus; }
            set
            {
                closeLostFocus = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("CloseLostFocus", value.ToString());
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
        public static int FontSizeS
        {
            get { return fontSizes; }
            set
            {
                fontSizes = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("FontSizeS", value.ToString());
            }
        }
        public static string FontFace
        {
            get { return fontFace; }
            set
            {
                fontFace = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("FontFace", value.ToString());
            }
        }
        public static ShortcutKeySet ShortcutKey
        {
            get { return shortcutKey; }
            set
            {
                shortcutKey = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("ShortcutKey", JsonSerializer.Serialize(value));

                mainWindow.SetupHoykey();
            }
        }

        public static bool IsTesseractInstalled(int accID, int langID)
        {
            RegistryKey key = RegLoadedTesseract.CreateSubKey(accID.ToString());
            var reg = key.GetValue(LanguageList.GetTesseractTagFromID(langID));

            return reg != null;
        }

        public static void SaveTesseractInstalled(int accID, int langID)
        {
            RegistryKey key = RegLoadedTesseract.CreateSubKey(accID.ToString());
            key.SetValue(LanguageList.GetTesseractTagFromID(langID), true);
        }

        public static bool IsHunspellInstalled(int langID)
        {
            var reg = RegLoadedHunspell.GetValue(langID.ToString());

            return reg != null;
        }

        public static void SaveHunspellInstalled(int langID)
        {
            RegLoadedHunspell.SetValue(langID.ToString(), true);
        }

        public static void Reset()
        {
            Setting.ScreenLookupReg.DeleteSubKeyTree("");
        }
    }
}
