using GTranslate.Translators;
using System.Globalization;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    internal class LanguageList
    {
        private static readonly Dictionary<string, string> displayNative = [];
        private static readonly Dictionary<string, string> displayName = [];
        public static List<dynamic> TranslatorService = [
            new GoogleTranslator(),
            new GoogleTranslator2(),
            new BingTranslator(),
            new MicrosoftTranslator(),
            new YandexTranslator(),
        ];

        public static string ClearTesseractTag(string tesseractTag)
        {
            tesseractTag = tesseractTag.Replace("_frak", "");
            tesseractTag = tesseractTag.Replace("_old", "");
            tesseractTag = tesseractTag.Replace("_latn", "");
            tesseractTag = tesseractTag.Replace("_vert", "");

            return tesseractTag;
        }

        public static string GetTesseractTagFromLanguageTesseract(string textName)
        {
            string tesseractTag = textName.Split('.').First();

            return tesseractTag;
        }

        public static string GetTesseractTagFromID(int langID)
        {
            string tesseractTag = GetTesseractTagFromLanguageTesseract(TesseractHelper.LangList[langID]);

            return tesseractTag;
        }

        public static string GetLanguageISO6391FromID(int langID)
        {
            string tessTag = ClearTesseractTag(LanguageList.GetTesseractTagFromID(langID));

            try
            {
                GLanguage languageData = tessTag switch
                {
                    "chi_sim" => GLanguage.GetLanguage("zh-Hans"),
                    "chi_tra" => GLanguage.GetLanguage("zh-Hant"),
                    _ => GLanguage.GetLanguage(tessTag)
                };
                return languageData.ISO6391;
            }
            catch
            {
                return tessTag.Substring(0, 2);
            }
        }

        public static string GetLanguageISO6393FromID(int langID)
        {
            string tessTag = ClearTesseractTag(LanguageList.GetTesseractTagFromID(langID));

            try
            {
                GLanguage languageData = tessTag switch
                {
                    "chi_sim" => GLanguage.GetLanguage("zh-Hans"),
                    "chi_tra" => GLanguage.GetLanguage("zh-Hant"),
                    _ => GLanguage.GetLanguage(tessTag)
                };
                return languageData.ISO6393;
            }
            catch
            {
                return tessTag.Substring(0, 3);
            }
        }

        public static string GetDisplayNameFromTesseractTag(string tesseractTag, bool isNative)
        {
            string tessLangTag = ClearTesseractTag(tesseractTag);

            if (isNative)
            {
                if (displayNative.TryGetValue(tessLangTag, out string name))
                {
                    return name;
                }
            }
            else
            {
                if (displayName.TryGetValue(tessLangTag, out string name))
                {
                    return name;
                }
            }

            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tessLangTag);
                string name = isNative ? languageData.NativeName : languageData.Name;

                if (isNative)
                    displayNative.TryAdd(tessLangTag, name);
                else
                    displayName.TryAdd(tessLangTag, name);

                return name;
            }
            catch
            {
                CultureInfo cultureInfo = tessLangTag switch
                {
                    "chi_sim" => new CultureInfo("zh-Hans"),
                    "chi_tra" => new CultureInfo("zh-Hant"),
                    _ => new CultureInfo(tessLangTag)
                };
                string note = "";

                if (tesseractTag == "dan_frak")
                    note = "(Fraktur)";

                if (tesseractTag == "deu_frak")
                    note = "(Fraktur)";

                if (tesseractTag == "ita_old")
                    note = "(Old)";

                if (tesseractTag == "kat_old")
                    note = "(Old)";

                if (tesseractTag == "slk_frak")
                    note = "(Fraktur)";

                if (tesseractTag == "spa_old")
                    note = "(Old)";

                if (tesseractTag == "srp_latn")
                    note = "(Latin)";

                if (tesseractTag.Contains("vert"))
                    note = "Vertical";

                if (tesseractTag.Contains("script"))
                    note = "Script";

                try
                {
                    GLanguage languageData = GLanguage.GetLanguage(cultureInfo.DisplayName);
                    string name = isNative ? $"{languageData.NativeName} {note}" : $"{languageData.Name} {note}";

                    if (isNative)
                        displayNative.TryAdd(tessLangTag, name);
                    else
                        displayName.TryAdd(tessLangTag, name);

                    return name;
                }
                catch
                {
                    string name = $"{cultureInfo.DisplayName} {note}";

                    if (isNative)
                        displayNative.TryAdd(tessLangTag, name);
                    else
                        displayName.TryAdd(tessLangTag, name);

                    return name;
                }
            }
        }

        public static string GetDisplayNameFromID(int langID, bool isNative)
        {
            string tessTag = GetTesseractTagFromID(langID);
            return GetDisplayNameFromTesseractTag(tessTag, isNative);
        }

        public static async Task<string> TranslatedText(string text, int targetLanguage)
        {
            // Translated text
            var translator = TranslatorService[App.setting.TranslationProvider];
            try
            {
                var translateResult = await translator.TranslateAsync(text, LanguageList.GetTesseractTagFromID(targetLanguage));
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
