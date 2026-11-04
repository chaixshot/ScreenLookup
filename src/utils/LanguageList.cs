using GTranslate.Translators;
using System.Globalization;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    internal static class LanguageList
    {
        private static readonly Dictionary<string, string> DisplayNative = [];
        private static readonly Dictionary<string, string> DisplayName = [];

        public static readonly string[] LangList = [
            "afr", "amh", "ara", "asm", "aze", "aze_cyrl", "bel", "ben", "bod", "bos",
            "bre", "bul", "cat", "ceb", "ces", "chi_sim", "chi_sim_vert", "chi_tra", "chi_tra_vert", "chr",
            "cos", "cym", "dan", "dan_frak", "deu", "deu_frak", "div", "dzo", "ell", "eng",
            "enm", "epo", "equ", "est", "eus", "fao", "fas", "fil", "fin", "fra",
            "frk", "frm", "fry", "gla", "gle", "glg", "grc", "guj", "hat", "heb",
            "hin", "hrv", "hun", "hye", "iku", "ind", "isl", "ita", "ita_old", "jav",
            "jpn", "jpn_vert", "kan", "kat", "kat_old", "kaz", "khm", "kir", "kmr", "kor",
            "kor_vert", "lao", "lat", "lav", "lit", "ltz", "mal", "mar", "mkd", "mlt",
            "mon", "mri", "msa", "mya", "nep", "nld", "nor", "oci", "ori", "osd",
            "pan", "pol", "por", "pus", "que", "ron", "rus", "san", "sin", "slk",
            "slk_frak", "slv", "snd", "spa", "spa_old", "sqi", "srp", "srp_latn", "sun", "swa",
            "swe", "syr", "tam", "tat", "tel", "tgk", "tgl", "tha", "tir", "ton",
            "tur", "uig", "ukr", "urd", "uzb", "uzb_cyrl", "vie", "yid", "yor"
        ];

        /// <summary>
        /// Removes known Tesseract tag suffixes from the specified tag string.
        /// </summary>
        public static string ClearLanguageTag(string tesseractTag)
        {
            tesseractTag = tesseractTag.Replace("_frak", string.Empty);
            tesseractTag = tesseractTag.Replace("_old", string.Empty);
            tesseractTag = tesseractTag.Replace("_latn", string.Empty);
            tesseractTag = tesseractTag.Replace("_vert", string.Empty);

            return tesseractTag;
        }

        /// <summary>
        /// Get Tesseract language tag from language ID (e.g. tha, eng, chi_sim)
        /// </summary>
        public static string GetTesseractTagFromID(int langID)
        {
            return LangList[langID];
        }

        /// <summary>
        /// Gets ISO-639-1 two-letter language code from language ID (e.g. th, en, zh)
        /// </summary>
        public static string GetLanguageISO6391FromID(int langID)
        {
            string tessTag = ClearLanguageTag(GetTesseractTagFromID(langID));

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
                return tessTag.Length >= 2 ? tessTag[..2] : tessTag;
            }
        }

        /// <summary>
        /// Gets ISO-639-3 three-letter language code from language ID
        /// </summary>
        public static string GetLanguageISO6393FromID(int langID)
        {
            string tessTag = ClearLanguageTag(GetTesseractTagFromID(langID));

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
                return tessTag.Length >= 3 ? tessTag[..3] : tessTag;
            }
        }

        /// <summary>
        /// Gets the human-readable display name for a given Tesseract language tag.
        /// </summary>
        public static string GetDisplayNameFromTesseractTag(string tesseractTag, bool isNative)
        {
            string tessLangTag = ClearLanguageTag(tesseractTag);

            if (isNative)
            {
                if (DisplayNative.TryGetValue(tessLangTag, out string? name))
                {
                    return name;
                }
            }
            else
            {
                if (DisplayName.TryGetValue(tessLangTag, out string? name))
                {
                    return name;
                }
            }

            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tessLangTag);
                string name = isNative ? languageData.NativeName : languageData.Name;

                if (isNative)
                    DisplayNative.TryAdd(tessLangTag, name);
                else
                    DisplayName.TryAdd(tessLangTag, name);

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
                string note = string.Empty;

                if (tesseractTag == "dan_frak" || tesseractTag == "deu_frak" || tesseractTag == "slk_frak")
                    note = "(Fraktur)";
                else if (tesseractTag == "ita_old" || tesseractTag == "kat_old" || tesseractTag == "spa_old")
                    note = "(Old)";
                else if (tesseractTag == "srp_latn")
                    note = "(Latin)";
                else if (tesseractTag.Contains("vert"))
                    note = "Vertical";
                else if (tesseractTag.Contains("script"))
                    note = "Script";

                try
                {
                    GLanguage languageData = GLanguage.GetLanguage(cultureInfo.DisplayName);
                    string name = isNative ? $"{languageData.NativeName} {note}".Trim() : $"{languageData.Name} {note}".Trim();

                    if (isNative)
                        DisplayNative.TryAdd(tessLangTag, name);
                    else
                        DisplayName.TryAdd(tessLangTag, name);

                    return name;
                }
                catch
                {
                    string name = $"{cultureInfo.DisplayName} {note}".Trim();

                    if (isNative)
                        DisplayNative.TryAdd(tessLangTag, name);
                    else
                        DisplayName.TryAdd(tessLangTag, name);

                    return name;
                }
            }
        }

        /// <summary>
        /// Returns the display name of a language corresponding to the specified language identifier.
        /// </summary>
        public static string GetDisplayNameFromID(int langID, bool isNative)
        {
            string tessTag = GetTesseractTagFromID(langID);
            return GetDisplayNameFromTesseractTag(tessTag, isNative);
        }

        public static ITranslator GetTranslatorService(ProviderService providerID)
        {
            return providerID switch
            {
                ProviderService.GoogleNew => new GoogleTranslator2(),
                ProviderService.Bing => new BingTranslator(),
                ProviderService.MicrosoftAzure => new MicrosoftTranslator(),
                ProviderService.Yandex => new YandexTranslator(),
                _ => new GoogleTranslator(),
            };
        }
    }
}
