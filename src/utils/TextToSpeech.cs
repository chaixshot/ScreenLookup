using NAudio.Wave;
using System.IO;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    class TextToSpeech
    {
        private static CancellationTokenSource CTS;
        private static readonly Dictionary<string, Stream> audioStreamCache = [];
        private static readonly Dictionary<string, CancellationTokenSource> audioStreamCTS = [];

        private static async Task<string> PlayTTS(string Text, int langID, CancellationTokenSource token)
        {
            var languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            var translator = LanguageList.GetTranslatorService(App.setting.TTSProvider);

            try
            {
                // Get sound stream
                if (!audioStreamCache.TryGetValue(Text, out Stream audioStream))
                {
                    Stream stream = await translator.TextToSpeechAsync(Text, languageData.ISO6393);
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
                if (audioStreamCTS.TryGetValue(Text, out CancellationTokenSource cancleToken))
                {
                    cancleToken.Cancel();
                    cancleToken.Dispose();
                    audioStreamCTS.Remove(Text);
                }
                cancleToken = new CancellationTokenSource();

                audioStreamCTS.TryAdd(Text, cancleToken);
                _ = Task.Delay(30 * 1000).ContinueWith((task) =>
                {
                    audioStreamCache[Text].Close();
                    audioStreamCache.Remove(Text);
                    audioStreamCTS.Remove(Text);
                }, cancleToken.Token);

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

        public static async void StartTTS(string Text, int langID, string window = "main")
        {
            StopTTS();
            CTS = new();

            var languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            string errorMsg = await Task.Run(() => PlayTTS(Text, langID, CTS));

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
            CTS?.Cancel();
        }
    }
}
