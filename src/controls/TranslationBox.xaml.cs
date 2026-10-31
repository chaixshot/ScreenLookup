using ScreenLookup.src.models;
using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;

namespace ScreenLookup.src.controls
{
    /// <summary>
    /// Interaction logic for TranslatedBox.xaml
    /// </summary>
    public partial class TranslationBox : UserControl
    {
        private string Original = string.Empty;
        public string Translated { get; private set; } = string.Empty;

        public Action<string>? TranslationCallBack { get; set; }

        private int SourceLanguage;
        private int TargetLanguage;
        private bool IsWord;

        private CancellationTokenSource _cts = new();

        public double ButtonWidth
        {
            get => (double)GetValue(ButtonWidthProperty);
            set => SetValue(ButtonWidthProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public Thickness OrientationMargin
        {
            get => (Thickness)GetValue(OrientationMarginProperty);
            private set => SetValue(OrientationMarginPropertyKey, value);
        }

        public static readonly DependencyProperty ButtonWidthProperty =
            DependencyProperty.Register(
                nameof(ButtonWidth),
                typeof(double),
                typeof(TranslationBox),
                new PropertyMetadata(App.setting.ButtonWidth)
            );

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(TranslationBox),
                new PropertyMetadata(Orientation.Horizontal, OnOrientationChanged)
            );

        private static readonly DependencyPropertyKey OrientationMarginPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(OrientationMargin),
                typeof(Thickness),
                typeof(TranslationBox),
                new PropertyMetadata(new Thickness(2, 0, 0, 0))
            );

        public static readonly DependencyProperty OrientationMarginProperty =
            OrientationMarginPropertyKey.DependencyProperty;

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TranslationBox box)
            {
                var orientation = (Orientation)e.NewValue;
                box.OrientationMargin = orientation == Orientation.Horizontal
                    ? new Thickness(2, 0, 0, 0)
                    : new Thickness(0, 2, 0, 0);
            }
        }

        public TranslationBox()
        {
            InitializeComponent();
            RestoreDefault();

            Unloaded += (s, e) =>
            {
                _cts?.Cancel();
            };
        }

        private void InitializeTranslation(bool isWord, string text, int sourceLang, int targetLang)
        {
            IsWord = isWord;
            Original = text;
            SourceLanguage = sourceLang;
            TargetLanguage = targetLang;
        }


        private void SetUIState(bool isLoading, bool showResult, bool showRefresh)
        {
            LoadingText.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            TranslatedBlock.Visibility = showResult ? Visibility.Visible : Visibility.Collapsed;
            Refresh.Visibility = showRefresh ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetOriginal(bool isWord, string text, int sourceLang, int targetLang)
        {
            InitializeTranslation(isWord, text, sourceLang, targetLang);
            SetUIState(isLoading: false, showResult: false, showRefresh: true);
        }

        public void SetTranslated(bool isWord, string text, int sourceLang, int targetLang)
        {
            InitializeTranslation(isWord, text, sourceLang, targetLang);

            TranslatedBlock.Text = text;
            Translated = text;

            SetUIState(isLoading: false, showResult: true, showRefresh: false);
        }

        public async Task<string> PerformTranslation(CancellationToken token)
        {
            SetUIState(isLoading: true, showResult: false, showRefresh: false);

            if (string.IsNullOrEmpty(Original))
            {
                SetUIState(isLoading: false, showResult: false, showRefresh: true);
                return string.Empty;
            }

            try
            {
                string result = await Translation.GetTranslated(IsWord, Original, SourceLanguage, TargetLanguage, token);

                if (token.IsCancellationRequested)
                    return string.Empty;

                if (string.IsNullOrEmpty(result))
                {
                    SetUIState(isLoading: false, showResult: false, showRefresh: true);
                    return string.Empty;
                }

                // Display Main Translation
                TranslatedBlock.Text = result;
                Translated = result;

                SetUIState(isLoading: false, showResult: true, showRefresh: false);
                TranslationCallBack?.Invoke(result);

                return result;
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                SnackbarHost.Show(title: "TranslationBox", message: ex.Message, type: SnackbarType.Error);
                SetUIState(isLoading: false, showResult: false, showRefresh: true);
                return string.Empty;
            }
        }

        public async void LoadExtraMeanings()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                LoadingExtraMeanings.Visibility = Visibility.Visible;
            });
        }

        public async void UpdateExtraMeanings(List<ExtraMeaningEntity> extraMeanings)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (extraMeanings != null && extraMeanings.Count > 0)
                {
                    ExtraMeaningsList.ItemsSource = extraMeanings;
                    ExtraMeaningsList.Visibility = Visibility.Visible;
                }
                else
                {
                    ExtraMeaningsList.ItemsSource = null;
                    ExtraMeaningsList.Visibility = Visibility.Collapsed;
                }

                LoadingExtraMeanings.Visibility = Visibility.Collapsed;
            });
        }

        public async void LoadPhonetic()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                LoadingPhonetic.Visibility = Visibility.Visible;
            });
        }

        public async void UpdatePhonetic(string phonetic)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(phonetic))
                {
                    PhoneticTextElement.Text = phonetic;
                    PhoneticTextElement.Visibility = Visibility.Visible;
                }
                else
                {
                    PhoneticTextElement.Text = string.Empty;
                    PhoneticTextElement.Visibility = Visibility.Collapsed;
                }

                LoadingPhonetic.Visibility = Visibility.Collapsed;
            });
        }

        public void RestoreDefault()
        {
            TranslatedBlock.Text = string.Empty;
            TranslatedBlock.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Visible;
            Refresh.Visibility = Visibility.Collapsed;

            PhoneticTextElement.Text = string.Empty;
            PhoneticTextElement.Visibility = Visibility.Collapsed;

            ExtraMeaningsList.ItemsSource = null;
            ExtraMeaningsList.Visibility = Visibility.Collapsed;

            UpdatePhonetic(string.Empty);

            translatedScrollViewer.ScrollToTop();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh.Visibility = Visibility.Collapsed;

            _cts?.Cancel();
            _cts = new();

            await PerformTranslation(_cts.Token);

            if (string.IsNullOrEmpty(Translated))
                Refresh.Visibility = Visibility.Visible;
        }

        private void SpeakButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Translated))
            {
                int lang = TargetLanguage != -1 ? TargetLanguage : SourceLanguage;
                TextToSpeech.StartTTS(Translated, lang);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Translated))
            {
                Clipboard.SetText(Translated);
                SnackbarHost.Show(title: "Copied", timeout: 1, width: 110, closeButton: false);
            }
        }
    }
}
