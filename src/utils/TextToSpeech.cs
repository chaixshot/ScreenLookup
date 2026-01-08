using NAudio.Wave;
using System.IO;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    class TextToSpeech
    {
        private static CancellationTokenSource PlayTTSCancelToken;
        private static readonly Dictionary<string, Stream> audioStreamCache = [];
        private static readonly Dictionary<string, CancellationTokenSource> audioStreamCTS = [];
        public static dynamic TextToSpeechProvider;

        static TextToSpeech()
        {
            ChangeTextToSpeechProvider(App.setting.TTSProvider);
        }

        private static async Task<string> PlayTTS(string Text, int langID, CancellationTokenSource token)
        {
            try
            {
                // Get sound stream
                if (!audioStreamCache.TryGetValue(Text, out Stream audioStream))
                {
                    Stream stream;
                    try
                    {
                        stream = await TextToSpeechProvider.TextToSpeechAsync(Text, LanguageList.GetLanguageISO6393FromID(langID));
                    }
                    catch
                    {
                        stream = await TextToSpeechProvider.TextToSpeechAsync(Text, LanguageList.GetLanguageISO6391FromID(langID));
                    }

                    audioStream = new MemoryStream();
                    byte[] buffer = new byte[32768];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        audioStream.Write(buffer, 0, read);
                    }
                    audioStreamCache.TryAdd(Text, audioStream);
                }

                // Release sound stream
                if (audioStreamCTS.TryGetValue(Text, out CancellationTokenSource cancelToken))
                {
                    cancelToken.Cancel();
                    cancelToken.Dispose();
                    audioStreamCTS.Remove(Text);
                }
                cancelToken = new CancellationTokenSource();

                audioStreamCTS.TryAdd(Text, cancelToken);
                _ = Task.Delay(30 * 1000).ContinueWith((task) =>
                {
                    audioStreamCache[Text].Close();
                    audioStreamCache.Remove(Text);
                    audioStreamCTS.Remove(Text);
                }, cancelToken.Token);

                // Play sound stream
                audioStream.Position = 0;
                using WaveStream blockAlignedStream =
                    new BlockAlignReductionStream(
                        WaveFormatConversionStream.CreatePcmStream(
                            new Mp3FileReader(audioStream)));
                WaveOut waveOut = new(WaveCallbackInfo.FunctionCallback());
                waveOut.Init(blockAlignedStream);
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
                waveOut.Dispose();

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static void ChangeTextToSpeechProvider(int providerID)
        {
            TextToSpeechProvider?.Dispose();
            TextToSpeechProvider = LanguageList.GetTranslatorService(providerID);
        }

        public static async void StartTTS(string Text, int langID, string window = "main")
        {
            StopTTS();
            PlayTTSCancelToken = new();

            var languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            string errorMsg = await Task.Run(() => PlayTTS(Text, langID, PlayTTSCancelToken));

            if (!string.IsNullOrEmpty(errorMsg))
            {
                bool isCaptureWindow = window == "capture";
                if (errorMsg.Contains("Language not supported"))
                    SnackbarHost.Show("Error", $"\"{languageData.NativeName}\" not supported Text-To-Speech via \"{App.setting.ProviderServices[App.setting.TTSProvider]}\"", type: "error", windows: isCaptureWindow ? "capture" : "main");
                else
                    SnackbarHost.Show("Error", errorMsg, type: "error", windows: isCaptureWindow ? "capture" : "main");
            }
        }

        public static void StopTTS()
        {
            PlayTTSCancelToken?.Cancel();
        }
    }
}
