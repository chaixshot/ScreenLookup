using GTranslate.Translators;
using System.Globalization;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    internal class LanguageList
    {
        private static readonly Dictionary<string, string> displayNative = [];
        private static readonly Dictionary<string, string> displayName = [];

        /// <summary>
        /// Removes known Tesseract tag suffixes from the specified tag string.
        /// </summary>
        /// <remarks>This method is useful for normalizing Tesseract tag names by stripping common variant
        /// suffixes. The comparison is case-sensitive.</remarks>
        /// <param name="tesseractTag">The tag string from which to remove Tesseract-specific suffixes. Cannot be null.</param>
        /// <returns>A string with the '_frak', '_old', '_latn', and '_vert' suffixes removed from the original tag, if present.</returns>
        public static string ClearTesseractTag(string tesseractTag)
        {
            tesseractTag = tesseractTag.Replace("_frak", "");
            tesseractTag = tesseractTag.Replace("_old", "");
            tesseractTag = tesseractTag.Replace("_latn", "");
            tesseractTag = tesseractTag.Replace("_vert", "");

            return tesseractTag;
        }

        /// <summary>
        /// Get Tesseract language tag from language Tesseract file name
        /// </summary>
        /// <param name="textName"></param>
        /// <returns>Tesseract tag (tha, eng, chi_sim)</returns>
        public static string GetTesseractTagFromLanguageTesseract(string textName)
        {
            string tesseractTag = textName.Split('.').First();

            return tesseractTag;
        }

        /// <summary>
        /// Get Tesseract language tag from language ID
        /// </summary>
        /// <param name="langID"></param>
        /// <returns>Tesseract tag (tha, eng, chi_sim)</returns>
        public static string GetTesseractTagFromID(int langID)
        {
            string tesseractTag = GetTesseractTagFromLanguageTesseract(TesseractHelper.LangList[langID]);

            return tesseractTag;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="langID"></param>
        /// <returns>th, en, ch</returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="langID"></param>
        /// <returns>tha, eng, chi</returns>
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

        /// <summary>
        /// Gets the human-readable display name for a given Tesseract language tag.
        /// </summary>
        /// <remarks>The method attempts to resolve the display name using internal mappings and language
        /// data. If the tag is not found, it falls back to using culture information and may append notes such as
        /// "(Fraktur)", "(Old)", or "Vertical" for certain tags. The result is cached for future calls.</remarks>
        /// <param name="tesseractTag">The Tesseract language tag to convert to a display name. This value should correspond to a valid Tesseract
        /// language code.</param>
        /// <param name="isNative">true to return the display name in the language's native form; otherwise, false to return the name in
        /// English.</param>
        /// <returns>A string containing the display name corresponding to the specified Tesseract language tag. If the tag is
        /// not recognized, returns a best-effort display name based on available information.</returns>
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

        /// <summary>
        /// Returns the display name of a language corresponding to the specified language identifier.
        /// </summary>
        /// <param name="langID">The identifier of the language for which to retrieve the display name.</param>
        /// <param name="isNative">true to return the display name in the language's native form; otherwise, false to return the name in
        /// English.</param>
        /// <returns>A string containing the display name of the specified language. Returns an empty string if the language
        /// identifier is not recognized.</returns>
        public static string GetDisplayNameFromID(int langID, bool isNative)
        {
            string tessTag = GetTesseractTagFromID(langID);
            return GetDisplayNameFromTesseractTag(tessTag, isNative);
        }

        public static dynamic GetTranslatorService(int providerID)
        {
            return providerID switch
            {
                1 => new GoogleTranslator2(),
                2 => new BingTranslator(),
                3 => new MicrosoftTranslator(),
                4 => new YandexTranslator(),
                _ => new GoogleTranslator(),
            };
        }
    }
}
