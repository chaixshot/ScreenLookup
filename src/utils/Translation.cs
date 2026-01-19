namespace ScreenLookup.src.utils
{
    internal class Translation
    {
        public static dynamic TranslationProvider;

        static Translation()
        {
            ChangeTranslationProvider(App.setting.TranslationProvider);
        }

        /// <summary>
        /// Changes the current translation provider to the provider specified by the given identifier.
        /// </summary>
        /// <remarks>If a translation provider is already in use, it is disposed before switching to the
        /// new provider. This method should be called when the application needs to switch translation services at
        /// runtime.</remarks>
        /// <param name="providerID">The unique identifier of the translation provider to use. Must correspond to a valid provider supported by
        /// the application.</param>
        public static void ChangeTranslationProvider(int providerID)
        {
            TranslationProvider?.Dispose();
            TranslationProvider = LanguageList.GetTranslatorService(providerID);
        }

        /// <summary>
        /// Translates the specified text from the source language to the target language asynchronously.
        /// </summary>
        /// <param name="text">The text to translate. Cannot be null.</param>
        /// <param name="sourceLang">The identifier of the source language. Must correspond to a supported language.</param>
        /// <param name="targetLang">The identifier of the target language. Must correspond to a supported language.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the translated text, or an empty
        /// string if the translation fails.</returns>
        public static async Task<string> GetTranslated(string text, int sourceLang, int targetLang)
        {
            // Translated text
            try
            {
                var translateResult = await TranslationProvider.TranslateAsync(text, LanguageList.GetTesseractTagFromID(targetLang), LanguageList.GetTesseractTagFromID(sourceLang));
                return translateResult.Translation;
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("Translation Error", ex.StackTrace, SnackbarType.Error);
                return "";
            }
        }
    }
}
