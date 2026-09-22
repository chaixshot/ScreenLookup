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

                // Play sound stream.
                long resumePosition = 0;
                bool keepPlaying = true;

                while (keepPlaying && !token.IsCancellationRequested)
                {
                    audioStream.Position = 0;
                    using WaveStream blockAlignedStream =
                        new BlockAlignReductionStream(
                            WaveFormatConversionStream.CreatePcmStream(
                                new Mp3FileReader(audioStream)));

                    // Seek to resume position after a device swap
                    if (resumePosition > 0 && blockAlignedStream.CanSeek)
                        blockAlignedStream.Position = Math.Min(resumePosition, blockAlignedStream.Length);

                    // WaveOutEvent drives its own thread — safe on background Task threads
                    // and always targets device -1 (current Windows default output).
                    using WaveOutEvent waveOut = new() { DeviceNumber = -1 };
                    try
                    {
                        waveOut.Init(blockAlignedStream);
                        waveOut.Play();

                        while (waveOut.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                        {
                            await Task.Delay(100);
                        }
                        keepPlaying = false; // Finished or cancelled normally
                    }
                    catch (NAudio.MmException ex) when (
                        ex.Result == NAudio.MmResult.InvalidHandle ||
                        ex.Result == NAudio.MmResult.BadDeviceId ||
                        ex.Result == NAudio.MmResult.NoDriver)
                    {
                        // Device changed mid-playback — save position and retry on new device
                        resumePosition = blockAlignedStream.Position;
                        await Task.Delay(300); // Wait for the new device to settle
                    }
                }


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

        public static async void StartTTS(string Text, int langID)
        {
            StopTTS();
            PlayTTSCancelToken = new();

            GLanguage languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            string errorMsg = await Task.Run(() => PlayTTS(Text, langID, PlayTTSCancelToken));

            if (!string.IsNullOrEmpty(errorMsg))
            {
                if (errorMsg.Contains("Language not supported"))
                    SnackbarHost.Show("Error", $"\"{languageData.NativeName}\" not supported Text-To-Speech via \"{App.setting.ProviderServices[App.setting.TTSProvider]}\"", type: SnackbarType.Error);
                else
                    SnackbarHost.Show("Error", errorMsg, type: SnackbarType.Error);
            }
        }

        public static void StopTTS()
        {
            PlayTTSCancelToken?.Cancel();
        }
    }
}
