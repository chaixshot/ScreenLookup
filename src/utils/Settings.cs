using Microsoft.Win32;
using ScreenLookup.src.models;
using System.Text.Json;
using System.Windows.Input;

namespace ScreenLookup.src.utils
{
    public class Settings
    {
        public static RegistryKey ScreenLookupReg = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup");
        public static readonly RegistryKey RegSetting = ScreenLookupReg.CreateSubKey("Settings");
        public readonly RegistryKey RegWindowBounds = ScreenLookupReg.CreateSubKey("WindowBounds");
        public readonly RegistryKey RegLoadedTesseract = ScreenLookupReg.CreateSubKey("InstalledTesseract");
        public readonly RegistryKey RegLoadedHunspell = ScreenLookupReg.CreateSubKey("InstalledHunspell");
        public readonly RegistryKey RegAutorun = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

        public bool topmost = RegSetting.GetValue("Topmost") == null || RegSetting.GetValue("Topmost").ToString() == "True";
        public bool startupWithWindows = RegSetting.GetValue("StartupWithWindows") == null || RegSetting.GetValue("StartupWithWindows").ToString() == "True";
        public bool startInBackground = RegSetting.GetValue("StartInBackground") != null && RegSetting.GetValue("StartInBackground").ToString() == "True";
        public bool minimizeToTray = RegSetting.GetValue("MinimizeToTray") == null || RegSetting.GetValue("MinimizeToTray").ToString() == "True";

        public int sourceLanguageAccuracy = RegSetting.GetValue("SourceLanguageAccuracy") != null ? Convert.ToInt32(RegSetting.GetValue("SourceLanguageAccuracy")) : 1;
        public int sourceLanguage = RegSetting.GetValue("SourceLanguage") != null ? Convert.ToInt32(RegSetting.GetValue("SourceLanguage")) : 29;
        public bool hunSpell = RegSetting.GetValue("hunSpell") != null && RegSetting.GetValue("hunSpell").ToString() == "True";
        public int targetLanguage = RegSetting.GetValue("TargetLanguage") != null ? Convert.ToInt32(RegSetting.GetValue("TargetLanguage")) : 117;
        public int translationProvider = RegSetting.GetValue("TranslationProvider") != null ? Convert.ToInt32(RegSetting.GetValue("TranslationProvider")) : 1;
        public int ttsProvider = RegSetting.GetValue("TTSProvider") != null ? Convert.ToInt32(RegSetting.GetValue("TTSProvider")) : 1;

        public ShortcutKeySet shortcutKey = RegSetting.GetValue("ShortcutKey") != null ? JsonSerializer.Deserialize<ShortcutKeySet>(RegSetting.GetValue("ShortcutKey").ToString()) : new ShortcutKeySet()
        {
            Modifiers = { ModifierKeys.Alt },
            NonModifierKey = Key.Z,
        };
        public static bool showImage = RegSetting.GetValue("ShowImage") == null || RegSetting.GetValue("ShowImage").ToString() == "True";
        public static bool showHighlight = RegSetting.GetValue("ShowHighlight") == null || RegSetting.GetValue("ShowHighlight").ToString() == "True";
        public static bool closeLostFocus = RegSetting.GetValue("CloseLostFocus") == null || RegSetting.GetValue("CloseLostFocus").ToString() == "True";
        public static string fontFace = RegSetting.GetValue("FontFace") != null ? RegSetting.GetValue("FontFace").ToString() : "Segoe UI";
        public static int fontSizes = RegSetting.GetValue("FontSizeS") != null ? Convert.ToInt32(RegSetting.GetValue("FontSizeS")) : 14;

        public readonly string[] ProviderServices = [
            "Google",
            "Google New",
            "Bing",
            "Microsoft Azure",
            "Yandex",
        ];

        public readonly string[] SourceAccuracys = [
            "Fast (Bad)",
            "Normal",
            "Slow (Accurate)",
        ];

        public int SourceLanguageAccuracy
        {
            get { return sourceLanguageAccuracy; }
            set
            {
                sourceLanguageAccuracy = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("SourceLanguageAccuracy", value.ToString());
            }
        }

        public int SourceLanguage
        {
            get { return sourceLanguage; }
            set
            {
                sourceLanguage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("SourceLanguage", value.ToString());
            }
        }

        public bool HunSpell
        {
            get { return hunSpell; }
            set
            {
                hunSpell = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("HunSpell", value.ToString());
            }
        }

        public int TargetLanguage
        {
            get { return targetLanguage; }
            set
            {
                targetLanguage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TargetLanguage", value.ToString());
            }
        }

        public int TranslationProvider
        {
            get { return translationProvider; }
            set
            {
                translationProvider = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TranslationProvider", value.ToString());
            }
        }
        public int TTSProvider
        {
            get { return ttsProvider; }
            set
            {
                ttsProvider = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("TTSProvider", value.ToString());
            }
        }

        public bool StartupWithWindows
        {
            get { return startupWithWindows; }
            set
            {
                startupWithWindows = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("StartupWithWindows", value.ToString());
            }
        }

        public bool StartInBackground
        {
            get { return startInBackground; }
            set
            {
                startInBackground = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("StartInBackground", value.ToString());
            }
        }

        public bool MinimizeToTray
        {
            get { return minimizeToTray; }
            set
            {
                minimizeToTray = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("MinimizeToTray", value.ToString());
            }
        }

        public bool ShowImage
        {
            get { return showImage; }
            set
            {
                showImage = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("ShowImage", value.ToString());
            }
        }

        public bool ShowHighlight
        {
            get { return showHighlight; }
            set
            {
                showHighlight = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("ShowHighlight", value.ToString());
            }
        }
        public bool CloseLostFocus
        {
            get { return closeLostFocus; }
            set
            {
                closeLostFocus = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("CloseLostFocus", value.ToString());
            }
        }

        public bool Topmost
        {
            get { return topmost; }
            set
            {
                topmost = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("Topmost", value.ToString());
            }
        }
        public int FontSizeS
        {
            get { return fontSizes; }
            set
            {
                fontSizes = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("FontSizeS", value.ToString());
            }
        }
        public string FontFace
        {
            get { return fontFace; }
            set
            {
                fontFace = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("FontFace", value.ToString());
            }
        }
        public ShortcutKeySet ShortcutKey
        {
            get { return shortcutKey; }
            set
            {
                shortcutKey = value;

                RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\ScreenLookup\\Settings\\");
                key.SetValue("ShortcutKey", JsonSerializer.Serialize(value));

                App.trayIcon.SetupHoykey();
            }
        }

        public bool IsTesseractInstalled(int accID, int langID)
        {
            RegistryKey key = RegLoadedTesseract.CreateSubKey(accID.ToString());
            var reg = key.GetValue(LanguageList.GetTesseractTagFromID(langID));

            return reg != null;
        }

        public void SaveTesseractInstalled(int accID, int langID)
        {
            RegistryKey key = RegLoadedTesseract.CreateSubKey(accID.ToString());
            key.SetValue(LanguageList.GetTesseractTagFromID(langID), true);
        }

        public bool IsHunspellInstalled(int langID)
        {
            var reg = RegLoadedHunspell.GetValue(langID.ToString());

            return reg != null;
        }

        public void SaveHunspellInstalled(int langID)
        {
            RegLoadedHunspell.SetValue(langID.ToString(), true);
        }

        public void Reset()
        {
            ScreenLookupReg.DeleteSubKeyTree("");
        }
    }
}
