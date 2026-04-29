using Microsoft.Win32;
using ScreenLookup.src.models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace ScreenLookup.src.utils
{
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public static readonly FileInfo settingFile = new($"{App.appDataFolder}/setting.json");
        public readonly RegistryKey RegAutorun = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

        public bool firstRun = true;
        public bool topmost = true;
        public bool startupWithWindows = true;
        public bool startInBackground = false;
        public bool minimizeToTray = true;

        public int sourceLanguageAccuracy = 1;
        public int sourceLanguage = 29;
        public bool hunSpell = false;
        public int targetLanguage = 117;
        public int translationProvider = 1;
        public int ttsProvider = 1;

        public ShortcutKeySet shortcutKey = new()
        {
            Modifiers = { ModifierKeys.Alt },
            NonModifierKey = Key.Z,
        };
        public bool lookupOnImage = true;
        public bool showImage = true;
        public bool showAuxiliary = true;
        public bool showHighlight = true;
        public bool closeLostFocus = true;
        public string fontFace = "Segoe UI";
        public int fontSizes = 14;

        public Dictionary<string, string> window = [];
        public Dictionary<string, bool> loadedTesseract = [];
        public Dictionary<string, bool> loadedHunspell = [];

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

        public void Load()
        {
            Settings settings;

            if (settingFile.Exists)
            {
                using FileStream fileStream = File.Open(settingFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                settings = JsonSerializer.Deserialize<Settings>(fileStream, new JsonSerializerOptions() { WriteIndented = true }) ?? new();
                fileStream.Close();

                FirstRun = settings.FirstRun;
                Topmost = settings.Topmost;
                StartupWithWindows = settings.StartupWithWindows;
                StartInBackground = settings.StartInBackground;
                MinimizeToTray = settings.MinimizeToTray;

                SourceLanguageAccuracy = settings.SourceLanguageAccuracy;
                SourceLanguage = settings.SourceLanguage;
                HunSpell = settings.HunSpell;
                TargetLanguage = settings.TargetLanguage;
                TranslationProvider = settings.TranslationProvider;
                TTSProvider = settings.TTSProvider;

                ShortcutKey = settings.ShortcutKey;
                LookupOnImage = settings.LookupOnImage;
                ShowImage = settings.ShowImage;
                ShowAuxiliary = settings.ShowAuxiliary;
                ShowHighlight = settings.ShowHighlight;
                CloseLostFocus = settings.CloseLostFocus;
                FontFace = settings.FontFace;
                FontSizeS = settings.FontSizeS;

                Window = settings.Window;
                LoadedTesseract = settings.loadedTesseract;
                LoadedHunspell = settings.loadedHunspell;
            }
        }

        public void Save()
        {
            using FileStream fileStream = File.Open(settingFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions() { WriteIndented = true });
            fileStream.Close();
        }

        public static void Reset()
        {
            settingFile.Delete();
        }

        #region

        public int SourceLanguageAccuracy
        {
            get => sourceLanguageAccuracy;
            set
            {
                sourceLanguageAccuracy = value;

                App.captureWindow.LoadInstalledLanguage();
                App.captureWindow.CreateTesseractEngine();

                App.settingPage?.LoadSourceLanguageContent();

                OnPropertyChanged();
            }
        }

        public int SourceLanguage
        {
            get => sourceLanguage;
            set
            {
                sourceLanguage = value;

                if (!HunspellHelper.IsInstalled(sourceLanguage))
                    HunSpell = false;

                App.captureWindow.CreateTesseractEngine();
                App.captureWindow.SelectConfigLanguage();

                App.settingPage?.SelectSourceLanguage();

                OnPropertyChanged();
            }
        }

        public bool HunSpell
        {
            get => hunSpell;
            set
            {
                if (value == true)
                {
                    if (HunspellHelper.IsInstalled(SourceLanguage))
                    {
                        hunSpell = true;
                        HunspellHelper.CreateHunspellEngine(SourceLanguage);
                    }
                }
                else
                {
                    hunSpell = false;
                    HunspellHelper.RemoveHunspellEngine();
                }

                OnPropertyChanged();
            }
        }

        public int TargetLanguage
        {
            get => targetLanguage;
            set
            {
                targetLanguage = value;

                OnPropertyChanged();
            }
        }

        public int TranslationProvider
        {
            get => translationProvider;
            set
            {
                translationProvider = value;

                Translation.ChangeTranslationProvider(value);

                OnPropertyChanged();
            }
        }

        public int TTSProvider
        {
            get => ttsProvider;
            set
            {
                ttsProvider = value;

                TextToSpeech.ChangeTextToSpeechProvider(value);


                OnPropertyChanged();
            }
        }

        public bool StartupWithWindows
        {
            get => startupWithWindows;
            set
            {
                startupWithWindows = value;

                if (startupWithWindows)
                    App.setting.RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
                else
                    App.setting.RegAutorun.DeleteValue("ScreenLookup", false);

                OnPropertyChanged();
            }
        }

        public bool StartInBackground
        {
            get => startInBackground;
            set
            {
                startInBackground = value;

                OnPropertyChanged();
            }
        }

        public bool MinimizeToTray
        {
            get => minimizeToTray;
            set
            {
                minimizeToTray = value;

                OnPropertyChanged();
            }
        }

        public bool LookupOnImage
        {
            get => lookupOnImage;
            set
            {
                lookupOnImage = value;

                OnPropertyChanged();
            }
        }

        public bool ShowImage
        {
            get => showImage;
            set
            {
                showImage = value;

                OnPropertyChanged();
            }
        }

        public bool ShowAuxiliary
        {
            get => showAuxiliary;
            set
            {
                showAuxiliary = value;

                OnPropertyChanged();
            }
        }

        public bool ShowHighlight
        {
            get => showHighlight;
            set
            {
                showHighlight = value;

                OnPropertyChanged();
            }
        }

        public bool CloseLostFocus
        {
            get => closeLostFocus;
            set
            {
                closeLostFocus = value;

                OnPropertyChanged();
            }
        }

        public bool FirstRun
        {
            get => firstRun;
            set
            {
                firstRun = value;

                OnPropertyChanged();
            }
        }

        public bool Topmost
        {
            get => topmost;
            set
            {
                topmost = value;

                OnPropertyChanged();
            }
        }

        public int FontSizeS
        {
            get => fontSizes;
            set
            {
                fontSizes = value;

                OnPropertyChanged();
            }
        }

        public string FontFace
        {
            get => fontFace;
            set
            {
                fontFace = value;

                OnPropertyChanged();
            }
        }

        public ShortcutKeySet ShortcutKey
        {
            get => shortcutKey;
            set
            {
                shortcutKey = value;

                App.SetupHoykey();

                OnPropertyChanged();
            }
        }

        public Dictionary<string, string> Window
        {
            get => window;
            set
            {
                window = value;

                OnPropertyChanged();
            }
        }

        public Dictionary<string, bool> LoadedTesseract
        {
            get => loadedTesseract;
            set
            {
                loadedTesseract = value;

                OnPropertyChanged();
            }
        }

        public Dictionary<string, bool> LoadedHunspell
        {
            get => loadedHunspell;
            set
            {
                loadedHunspell = value;

                OnPropertyChanged();
            }
        }
        #endregion

        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Save();
        }
    }
}
