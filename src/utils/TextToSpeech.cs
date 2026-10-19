using NAudio.Wave;
using ScreenLookup.src.utils.Database;
using System.IO;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    /// <summary>
    /// Utility class for Text-To-Speech audio playback and provider management.
    /// </summary>
    internal static class TextToSpeech
    {
        #region Fields & Initialization
        private static CancellationTokenSource? _playTTSCancelToken;
        private static readonly Dictionary<string, Stream> AudioStreamCache = [];
        private static readonly Dictionary<string, CancellationTokenSource> AudioStreamCTS = [];
        public static dynamic? TextToSpeechProvider;

        static TextToSpeech()
        {
            ChangeTextToSpeechProvider(App.setting.TTSProvider);
        }

        public static void ChangeTextToSpeechProvider(int providerID)
        {
            TextToSpeechProvider?.Dispose();
            TextToSpeechProvider = LanguageList.GetTranslatorService(providerID);
        }
        #endregion

        #region Public TTS Controls
        public static async void StartTTS(string text, int langID)
        {
            if (string.IsNullOrEmpty(text))
                return;

            StopTTS();
            _playTTSCancelToken = new CancellationTokenSource();
            CancellationToken cancellationToken = _playTTSCancelToken.Token;

            IEnumerable<string> sentences = [.. SplitIntoSentences(text)];
            GLanguage languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            string? errorMsg = null;

            foreach (string sentence in sentences)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                errorMsg = await Task.Run(() => PlayTTS(sentence, langID, cancellationToken), cancellationToken);
                if (!string.IsNullOrEmpty(errorMsg))
                    break;
            }

            if (!string.IsNullOrEmpty(errorMsg))
            {
                if (errorMsg.Contains("Language not supported"))
                    SnackbarHost.Show("Error", $"\"{languageData.NativeName}\" does not support Text-To-Speech via \"{App.setting.ProviderServices[App.setting.TTSProvider]}\"", type: SnackbarType.Error);
                else
                    SnackbarHost.Show("Error", errorMsg, type: SnackbarType.Error);
            }
        }

        public static void StopTTS()
        {
            _playTTSCancelToken?.Cancel();
        }
        #endregion

        #region Private Audio Stream Playback & Sentence Chunking
        private static IEnumerable<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            string[]? rawSentences = AppUtilities.RegexPunctuation().Split(text);
            foreach (string s in rawSentences)
            {
                string trimmed = s.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Do not output chunks that consist entirely of punctuation or symbols
                if (trimmed.All(c => char.IsPunctuation(c) || char.IsSymbol(c) || char.IsWhiteSpace(c)))
                    continue;

                // Split long sentences if they exceed 250 characters
                if (trimmed.Length > 250)
                {
                    for (int i = 0; i < trimmed.Length; i += 250)
                        yield return trimmed.Substring(i, Math.Min(250, trimmed.Length - i));
                }
                else
                    yield return trimmed;
            }
        }

        private static async Task<string?> PlayTTS(string text, int langID, CancellationToken token)
        {
            try
            {
                int ttsProviderID = App.setting.TTSProvider;

                // Get or fetch sound stream
                if (!AudioStreamCache.TryGetValue(text, out Stream? audioStream))
                {
                    // Check local SQLite BLOB audio cache
                    byte[]? cachedAudio = await TTSCacheLogger.GetTtsAudioAsync(text, langID, ttsProviderID);
                    if (cachedAudio != null && cachedAudio.Length > 0)
                    {
                        audioStream = new MemoryStream(cachedAudio);
                        AudioStreamCache.TryAdd(text, audioStream);
                    }
                    else
                    {
                        // Fetch audio stream from TTS provider
                        Stream stream;
                        try
                        {
                            stream = await TextToSpeechProvider!.TextToSpeechAsync(text, LanguageList.GetLanguageISO6393FromID(langID));
                        }
                        catch
                        {
                            stream = await TextToSpeechProvider!.TextToSpeechAsync(text, LanguageList.GetLanguageISO6391FromID(langID));
                        }

                        MemoryStream memStream = new();
                        byte[] buffer = new byte[32768];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memStream.Write(buffer, 0, read);
                        }

                        audioStream = memStream;
                        AudioStreamCache.TryAdd(text, audioStream);

                        // Save audio bytes to SQLite database BLOB
                        byte[] audioBytes = memStream.ToArray();
                        _ = TTSCacheLogger.SaveTtsAudioAsync(text, langID, ttsProviderID, audioBytes);
                    }
                }

                // Manage stream cache expiration
                if (AudioStreamCTS.TryGetValue(text, out CancellationTokenSource? cancelToken))
                {
                    cancelToken.Cancel();
                    cancelToken.Dispose();
                    AudioStreamCTS.Remove(text);
                }
                cancelToken = new CancellationTokenSource();

                AudioStreamCTS.TryAdd(text, cancelToken);
                _ = Task.Delay(30 * 1000, cancelToken.Token).ContinueWith(_ =>
                {
                    if (AudioStreamCache.TryGetValue(text, out Stream? stream))
                    {
                        stream.Close();
                        AudioStreamCache.Remove(text);
                    }
                    AudioStreamCTS.Remove(text);
                }, cancelToken.Token);

                // Play sound stream
                long resumePosition = 0;
                bool keepPlaying = true;

                while (keepPlaying && !token.IsCancellationRequested)
                {
                    audioStream.Position = 0;
                    using WaveStream blockAlignedStream = new BlockAlignReductionStream(
                        WaveFormatConversionStream.CreatePcmStream(new Mp3FileReader(audioStream)));

                    if (resumePosition > 0 && blockAlignedStream.CanSeek)
                        blockAlignedStream.Position = Math.Min(resumePosition, blockAlignedStream.Length);

                    using WaveOutEvent waveOut = new() { DeviceNumber = -1 };
                    try
                    {
                        waveOut.Init(blockAlignedStream);
                        waveOut.Play();

                        while (waveOut.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                            await Task.Delay(100, token);

                        if (token.IsCancellationRequested)
                            waveOut.Stop();
                        keepPlaying = false;
                    }
                    catch (NAudio.MmException ex) when (
                        ex.Result == NAudio.MmResult.InvalidHandle ||
                        ex.Result == NAudio.MmResult.BadDeviceId ||
                        ex.Result == NAudio.MmResult.NoDriver ||
                        ex.Result == NAudio.MmResult.MemoryAllocationError)
                    {
                        resumePosition = blockAlignedStream.Position;
                        await Task.Delay(300, token);
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        #endregion
    }
}
