using NAudio.Wave;
using System.IO;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    class TextToSpeech
    {
        public static CancellationTokenSource CTS = new CancellationTokenSource();
        public static readonly Dictionary<string, Stream> audioStreamCache = [];
        public static readonly Dictionary<string, CancellationTokenSource> audioStreamCTS = [];

        public static async void PlayTTS(string Text, int langID, CancellationTokenSource token)
        {
            var languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            var translator = LanguageList.GetTranslatorService(Setting.TTSProvider);

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
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Language not supported"))
                    Notification.Show($"\"{languageData.NativeName}\" not supported Text-To-Speech via \"{Setting.ProviderServices[Setting.TTSProvider]}\"");
                else
                    Notification.Show(ex.Message);
            }
        }

        public static void StartTTS(string Text, int langID)
        {
            StopTTS();
            CTS = new();
            TextToSpeech.PlayTTS(Text, langID, CTS);
        }

        public static void StopTTS()
        {
            CTS.Cancel();
        }
    }
}
