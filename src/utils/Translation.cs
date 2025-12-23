namespace ScreenLookup.src.utils
{
    internal class Translation
    {
        public static dynamic TranslationProvider;

        static Translation()
        {
            ChangeTranslationProvider(App.setting.TranslationProvider);
        }

        public static void ChangeTranslationProvider(int providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }

        public static async Task<string> GetTranslated(string text, int targetLanguage)
        {
            // Translated text
            try
            {
                var translateResult = await TranslationProvider.TranslateAsync(text, LanguageList.GetTesseractTagFromID(targetLanguage));
                return translateResult.Translation;
            }
            catch (Exception ex)
            {
                Notification.Show(ex.Message);
                return "";
            }
        }
    }
}
