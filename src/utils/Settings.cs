using Microsoft.Win32;
using ScreenLookup.src.llmAPI;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using System.ComponentModel;
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

        #region Backing Fields
        private bool firstRun = true;
        private bool topmost = true;
        private bool startupWithWindows = true;
        private bool startInBackground = false;
        private bool minimizeToTray = true;

        private OcrHelper.OcrEngine ocrEngineMode = OcrHelper.OcrEngine.TesseraceOCR;
        private SourceAccuracy sourceLanguageAccuracy = SourceAccuracy.Normal;
        private int sourceLanguage = 29;
        private bool hunSpell = false;
        private int targetLanguage = 117;
        private ProviderService wordTranslationProvider = ProviderService.GoogleNew;
        private ProviderService paragraphTranslationProvider = ProviderService.GoogleNew;
        private ProviderService ttsProvider = ProviderService.GoogleNew;

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
        private bool showTranslation = true;
        private string fontFace = "Segoe UI";
        private int fontSizes = 14;

        public Dictionary<string, string> window = [];
        public Dictionary<string, bool> loadedTesseract = [];   // kept for migration
        public Dictionary<string, bool> loadedPaddle = [];
        public Dictionary<string, bool> loadedHunspell = [];

        private bool autoConnectStamVR = false;
        private bool overlayEnable = true;
        private float overlayHigh = 0f;
        private float overlayDistance = 2f;
        private float overlayScale = 2f;
        private int overlayScrollSpeed = 30;
        private int overlayCurve = 0;
        private int activationRadius = 15;
        private bool useHmdRotations = false;
        private float hmdRotationThreshold = 0.003f;
        private bool useRightEye = true;
        private int frameOffset = 5;

        public OllamaConfig OllamaConfig { get; set; } = new();
        public OpenAIConfig OpenAIConfig { get; set; } = new();
        public LMStudioConfig LMStudioConfig { get; set; } = new();
        public OpenRouterConfig OpenRouterConfig { get; set; } = new();
        public DeepLConfig DeepLConfig { get; set; } = new();
        public YoudaoConfig YoudaoConfig { get; set; } = new();
        public MTranServerConfig MTranServerConfig { get; set; } = new();
        public BaiduConfig BaiduConfig { get; set; } = new();
        public LibreTranslateConfig LibreTranslateConfig { get; set; } = new();
        #endregion

        #region Load & Save Methods
        public void Load()
        {
            if (settingFile.Exists)
            {
                try
                {
                    using FileStream fileStream = File.Open(settingFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Settings settings = JsonSerializer.Deserialize<Settings>(fileStream, new JsonSerializerOptions { WriteIndented = true }) ?? new();
                    fileStream.Close();

                    Window = settings.Window;
                    LoadedTesseract = settings.loadedTesseract;
                    LoadedPaddle = settings.loadedPaddle;
                    LoadedHunspell = settings.loadedHunspell;
                    if (settings.OllamaConfig != null) OllamaConfig = settings.OllamaConfig;
                    if (settings.OpenAIConfig != null) OpenAIConfig = settings.OpenAIConfig;
                    if (settings.LMStudioConfig != null) LMStudioConfig = settings.LMStudioConfig;
                    if (settings.OpenRouterConfig != null) OpenRouterConfig = settings.OpenRouterConfig;
                    if (settings.DeepLConfig != null) DeepLConfig = settings.DeepLConfig;
                    if (settings.YoudaoConfig != null) YoudaoConfig = settings.YoudaoConfig;
                    if (settings.MTranServerConfig != null) MTranServerConfig = settings.MTranServerConfig;
                    if (settings.BaiduConfig != null) BaiduConfig = settings.BaiduConfig;
                    if (settings.LibreTranslateConfig != null) LibreTranslateConfig = settings.LibreTranslateConfig;

                    FirstRun = settings.FirstRun;
                    Topmost = settings.Topmost;
                    StartupWithWindows = settings.StartupWithWindows;
                    StartInBackground = settings.StartInBackground;
                    MinimizeToTray = settings.MinimizeToTray;

                    SourceLanguageAccuracy = settings.SourceLanguageAccuracy;
                    SourceLanguage = settings.SourceLanguage;
                    OcrEngineMode = settings.OcrEngineMode;
                    HunSpell = settings.HunSpell;
                    TargetLanguage = settings.TargetLanguage;
                    WordTranslationProvider = settings.WordTranslationProvider;
                    ParagraphTranslationProvider = settings.ParagraphTranslationProvider != default ? settings.ParagraphTranslationProvider : settings.ParagraphTranslationProvider;
                    TTSProvider = settings.TTSProvider;

                    ShortcutKey = settings.ShortcutKey;
                    LookupOnImage = settings.LookupOnImage;
                    ShowImage = settings.ShowImage;
                    ShowAuxiliary = settings.ShowAuxiliary;
                    ShowHighlight = settings.ShowHighlight;
                    CloseLostFocus = settings.CloseLostFocus;
                    ShowTranslation = settings.ShowTranslation;
                    FontFace = settings.FontFace;
                    FontSizeS = settings.FontSizeS;

                    AutoConnectStamVR = settings.autoConnectStamVR;
                    OverlayEnable = settings.overlayEnable;
                    OverlayHigh = settings.overlayHigh;
                    OverlayDistance = settings.overlayDistance;
                    OverlayScale = settings.overlayScale;
                    OverlayScrollSpeed = settings.overlayScrollSpeed;
                    OverlayCurve = settings.overlayCurve;
                    ActivationRadius = settings.activationRadius;
                    UseHmdRotations = settings.useHmdRotations;
                    HmdRotationThreshold = settings.hmdRotationThreshold;
                    UseRightEye = settings.useRightEye;
                    FrameOffset = settings.frameOffset;
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("Settings", $"Failed to load settings: {ex.Message}", type: SnackbarType.Error);
                }
            }
        }

        private CancellationTokenSource? _saveCts;
        public async void Save()
        {
            _saveCts?.Cancel();
            var token = (_saveCts = new CancellationTokenSource()).Token;

            try
            {
                await Task.Delay(500, token);
                File.WriteAllText(settingFile.FullName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SnackbarHost.Show("Settings", $"Failed to save settings: {ex.Message}", type: SnackbarType.Error);
            }
        }

        public static void Reset()
        {
            if (settingFile.Exists)
                settingFile.Delete();
        }
        #endregion

        #region Properties
        public OcrHelper.OcrEngine OcrEngineMode
        {
            get => ocrEngineMode;
            set
            {
                ocrEngineMode = value;

                if (App.mainWindow?.IsLoaded == true)
                {
                    App.captureWindow.LoadInstalledLanguage();
                    OcrHelper.Dispose();

                    App.settingPage?.LoadSourceLanguageContent();
                    App.settingPage?.ButtonDownloadTesseractChanged();
                }

                OnPropertyChanged();
            }
        }

        public SourceAccuracy SourceLanguageAccuracy
        {
            get => sourceLanguageAccuracy;
            set
            {
                sourceLanguageAccuracy = value;

                if (App.mainWindow?.IsLoaded == true)
                {
                    App.captureWindow.LoadInstalledLanguage();
                    OcrHelper.Dispose();

                    App.settingPage?.LoadSourceLanguageContent();
                }

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

                if (App.mainWindow?.IsLoaded == true)
                {
                    OcrHelper.Dispose();
                    App.captureWindow.SelectConfigLanguage();

                    App.settingPage?.SelectSourceLanguage();
                }

                OnPropertyChanged();
            }
        }

        public bool HunSpell
        {
            get => hunSpell;
            set
            {
                hunSpell = value;

                if (App.mainWindow?.IsLoaded == true)
                {
                    if (value && HunspellHelper.IsInstalled(SourceLanguage))
                        HunspellHelper.CreateHunspellEngine(SourceLanguage);
                    else if (!value)
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

        public ProviderService WordTranslationProvider
        {
            get => wordTranslationProvider;
            set
            {
                wordTranslationProvider = value;

                if (App.mainWindow?.IsLoaded == true)
                    Translation.ChangeTranslationProvider(value);

                OnPropertyChanged();
            }
        }

        public ProviderService ParagraphTranslationProvider
        {
            get => paragraphTranslationProvider;
            set
            {
                paragraphTranslationProvider = value;
                OnPropertyChanged();
            }
        }

        public ProviderService TTSProvider
        {
            get => ttsProvider;
            set
            {
                ttsProvider = value;

                if (App.mainWindow?.IsLoaded == true)
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
                    RegAutorun.SetValue("ScreenLookup", $"\"{AppDomain.CurrentDomain.BaseDirectory}\\ScreenLookup.exe\"");
                else
                    RegAutorun.DeleteValue("ScreenLookup", false);

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

        public bool ShowTranslation
        {
            get => showTranslation;
            set
            {
                showTranslation = value;
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
                OnPropertyChanged(nameof(ButtonWidth));
            }
        }

        public bool AutoConnectStamVR
        {
            get => autoConnectStamVR;
            set
            {
                autoConnectStamVR = value;

                if (App.mainWindow?.IsLoaded == true && autoConnectStamVR)
                    FrameShotPage.AutoConnectSteamVR();

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

        public int OverlayScrollSpeed
        {
            get => overlayScrollSpeed;
            set
            {
                overlayScrollSpeed = value;
                OnPropertyChanged();
            }
        }

        public int OverlayCurve
        {
            get => overlayCurve;
            set
            {
                overlayCurve = value;
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

        public float HmdRotationThreshold
        {
            get => hmdRotationThreshold;
            set
            {
                hmdRotationThreshold = value;
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

        public int FrameOffset
        {
            get => frameOffset;
            set
            {
                frameOffset = value;
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

        public Dictionary<string, bool> LoadedPaddle
        {
            get => loadedPaddle;
            set
            {
                loadedPaddle = value;
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

        public double ButtonWidth => fontSizes + 10;

        #endregion

        #region INotifyPropertyChanged
        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            if (App.mainWindow?.IsLoaded == true)
            {
                Save();
            }
        }
        #endregion
    }
}
