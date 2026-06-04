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

        private bool firstRun = true;
        private bool topmost = true;
        private bool startupWithWindows = true;
        private bool startInBackground = false;
        private bool minimizeToTray = true;

        private int sourceLanguageAccuracy = 1;
        private int sourceLanguage = 29;
        private bool hunSpell = false;
        private int targetLanguage = 117;
        private int translationProvider = 1;
        private int ttsProvider = 1;

        private ShortcutKeySet shortcutKey = new()
        {
            Modifiers = { ModifierKeys.Alt },
            NonModifierKey = Key.Z,
        };
        private bool lookupOnImage = true;
        private bool showImage = true;
        private bool showAuxiliary = true;
        private bool showHighlight = true;
        private bool closeLostFocus = true;
        private string fontFace = "Segoe UI";
        private int fontSizes = 14;

        public Dictionary<string, string> window = [];
        public Dictionary<string, bool> loadedTesseract = [];
        public Dictionary<string, bool> loadedHunspell = [];

        private bool overlayEnable = true;
        private float overlayHigh = 0f;
        private float overlayDistance = 2f;
        private float overlayScale = 2f;
        private int activationRadius = 15;
        private bool useHmdRotations = false;
        private bool useRightEye = true;

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

                Window = settings.Window;
                LoadedTesseract = settings.loadedTesseract;
                LoadedHunspell = settings.loadedHunspell;

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

                OverlayEnable = settings.overlayEnable;
                OverlayHigh = settings.overlayHigh;
                OverlayDistance = settings.overlayDistance;
                OverlayScale = settings.overlayScale;
                ActivationRadius = settings.activationRadius;
                UseHmdRotations = settings.useHmdRotations;
                UseRightEye = settings.useRightEye;
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

        public bool OverlayEnable
        {
            get => overlayEnable;
            set
            {
                overlayEnable = value;
                OnPropertyChanged();
            }
        }

        public float OverlayHigh
        {
            get => overlayHigh;
            set
            {
                overlayHigh = value;
                OnPropertyChanged();
            }
        }

        public float OverlayDistance
        {
            get => overlayDistance;
            set
            {
                overlayDistance = value;
                OnPropertyChanged();
            }
        }

        public float OverlayScale
        {
            get => overlayScale;
            set
            {
                overlayScale = value;
                OnPropertyChanged();
            }
        }

        public int ActivationRadius
        {
            get => activationRadius;
            set
            {
                activationRadius = value;

                OnPropertyChanged();
            }
        }

        public bool UseHmdRotations
        {
            get => useHmdRotations;
            set
            {
                useHmdRotations = value;

                OnPropertyChanged();
            }
        }

        public bool UseRightEye
        {
            get => useRightEye;
            set
            {
                useRightEye = value;
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
