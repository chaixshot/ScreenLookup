using NAudio.Wave;
using System.IO;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    class TextToSpeech
    {
        public static CancellationTokenSource CTS = new CancellationTokenSource();

        public static async void PlayTTS(string Text, int langID, CancellationTokenSource token)
        {
            var languageData = GLanguage.GetLanguage(LanguageList.GetLanguageISO6391FromID(langID));
            var translator = LanguageList.GetTranslatorService(Setting.TTSProvider);

            try
            {
                Stream stream = await translator.TextToSpeechAsync(Text, languageData.ISO6393);

                Stream ms = new MemoryStream();
                byte[] buffer = new byte[32768];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                ms.Position = 0;
                using WaveStream blockAlignedStream =
                    new BlockAlignReductionStream(
                        WaveFormatConversionStream.CreatePcmStream(
                            new Mp3FileReader(ms)));
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
                Notification.Show(ex.Message);
            }
        }

        public static void StartTTS(string Text, int langID)
        {
            StopTTS();
            CTS = new CancellationTokenSource();
            TextToSpeech.PlayTTS(Text, langID, CTS);
        }

        public static void StopTTS()
        {
            CTS.Cancel();
        }
    }
}
