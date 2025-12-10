using GTranslate.Translators;
using System.Globalization;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.utils
{
    internal class LanguageList
    {
        public static readonly string[] LanguageTesseract = [
            "afr.traineddata",
            "amh.traineddata",
            "ara.traineddata",
            "asm.traineddata",
            "aze.traineddata",
            "aze_cyrl.traineddata",
            "bel.traineddata",
            "ben.traineddata",
            "bod.traineddata",
            "bos.traineddata",
            "bre.traineddata",
            "bul.traineddata",
            "cat.traineddata",
            "ceb.traineddata",
            "ces.traineddata",
            "chi_sim.traineddata",
            "chi_sim_vert.traineddata",
            "chi_tra.traineddata",
            "chi_tra_vert.traineddata",
            "chr.traineddata",
            "cos.traineddata",
            "cym.traineddata",
            "dan.traineddata",
            "dan_frak.traineddata",
            "deu.traineddata",
            "deu_frak.traineddata",
            "div.traineddata",
            "dzo.traineddata",
            "ell.traineddata",
            "eng.traineddata",
            "enm.traineddata",
            "epo.traineddata",
            "equ.traineddata",
            "est.traineddata",
            "eus.traineddata",
            "fao.traineddata",
            "fas.traineddata",
            "fil.traineddata",
            "fin.traineddata",
            "fra.traineddata",
            "frk.traineddata",
            "frm.traineddata",
            "fry.traineddata",
            "gla.traineddata",
            "gle.traineddata",
            "glg.traineddata",
            "grc.traineddata",
            "guj.traineddata",
            "hat.traineddata",
            "heb.traineddata",
            "hin.traineddata",
            "hrv.traineddata",
            "hun.traineddata",
            "hye.traineddata",
            "iku.traineddata",
            "ind.traineddata",
            "isl.traineddata",
            "ita.traineddata",
            "ita_old.traineddata",
            "jav.traineddata",
            "jpn.traineddata",
            "jpn_vert.traineddata",
            "kan.traineddata",
            "kat.traineddata",
            "kat_old.traineddata",
            "kaz.traineddata",
            "khm.traineddata",
            "kir.traineddata",
            "kmr.traineddata",
            "kor.traineddata",
            "kor_vert.traineddata",
            "lao.traineddata",
            "lat.traineddata",
            "lav.traineddata",
            "lit.traineddata",
            "ltz.traineddata",
            "mal.traineddata",
            "mar.traineddata",
            "mkd.traineddata",
            "mlt.traineddata",
            "mon.traineddata",
            "mri.traineddata",
            "msa.traineddata",
            "mya.traineddata",
            "nep.traineddata",
            "nld.traineddata",
            "nor.traineddata",
            "oci.traineddata",
            "ori.traineddata",
            "osd.traineddata",
            "pan.traineddata",
            "pol.traineddata",
            "por.traineddata",
            "pus.traineddata",
            "que.traineddata",
            "ron.traineddata",
            "rus.traineddata",
            "san.traineddata",
            "sin.traineddata",
            "slk.traineddata",
            "slk_frak.traineddata",
            "slv.traineddata",
            "snd.traineddata",
            "spa.traineddata",
            "spa_old.traineddata",
            "sqi.traineddata",
            "srp.traineddata",
            "srp_latn.traineddata",
            "sun.traineddata",
            "swa.traineddata",
            "swe.traineddata",
            "syr.traineddata",
            "tam.traineddata",
            "tat.traineddata",
            "tel.traineddata",
            "tgk.traineddata",
            "tgl.traineddata",
            "tha.traineddata",
            "tir.traineddata",
            "ton.traineddata",
            "tur.traineddata",
            "uig.traineddata",
            "ukr.traineddata",
            "urd.traineddata",
            "uzb.traineddata",
            "uzb_cyrl.traineddata",
            "vie.traineddata",
            "yid.traineddata",
            "yor.traineddata",
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
            string tesseractTag = GetTesseractTagFromLanguageTesseract(LanguageList.LanguageTesseract[langID]);

            return tesseractTag;
        }

        public static string GetLanguageISO6391FromID(int langID)
        {
            string tesseractTag = ClearTesseractTag(LanguageList.GetTesseractTagFromID(langID));

            try
            {
                GLanguage languageData = tesseractTag switch
                {
                    "chi_sim" => GLanguage.GetLanguage("zh-Hans"),
                    "chi_tra" => GLanguage.GetLanguage("zh-Hant"),
                    _ => GLanguage.GetLanguage(tesseractTag)
                };
                return languageData.ISO6391;
            }
            catch
            {
                return tesseractTag.Substring(0, 2);
            }
        }

        public static string GetLanguageISO6393FromID(int langID)
        {
            string tesseractTag = ClearTesseractTag(LanguageList.GetTesseractTagFromID(langID));

            try
            {
                GLanguage languageData = tesseractTag switch
                {
                    "chi_sim" => GLanguage.GetLanguage("zh-Hans"),
                    "chi_tra" => GLanguage.GetLanguage("zh-Hant"),
                    _ => GLanguage.GetLanguage(tesseractTag)
                };
                return languageData.ISO6393;
            }
            catch
            {
                return tesseractTag.Substring(0, 3);
            }
        }

        public static string GetDisplayNameFromTesseractTag(string tesseractTag, bool isNative)
        {
            string tessLangTag = ClearTesseractTag(tesseractTag);

            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tessLangTag);
                return isNative ? languageData.NativeName : languageData.Name;
            }
            catch
            {
                CultureInfo cultureInfo = tessLangTag switch
                {
                    "chi_sim" => new CultureInfo("zh-Hans"),
                    "chi_tra" => new CultureInfo("zh-Hant"),
                    _ => new CultureInfo(tessLangTag)
                };
                string DisplayName = cultureInfo.DisplayName;

                if (tesseractTag == "dan_frak")
                    DisplayName = $"{cultureInfo.DisplayName} (Fraktur)";

                if (tesseractTag == "deu_frak")
                    DisplayName = $"{cultureInfo.DisplayName} (Fraktur)";

                if (tesseractTag == "ita_old")
                    DisplayName = $"{cultureInfo.DisplayName} (Old)";

                if (tesseractTag == "kat_old")
                    DisplayName = $"{cultureInfo.DisplayName} (Old)";

                if (tesseractTag == "slk_frak")
                    DisplayName = $"{cultureInfo.DisplayName} (Fraktur)";

                if (tesseractTag == "spa_old")
                    DisplayName = $"{cultureInfo.DisplayName} (Old)";

                if (tesseractTag == "srp_latn")
                    DisplayName = $"{cultureInfo.DisplayName} (Latin)";

                if (tesseractTag.Contains("vert"))
                    DisplayName = $"{cultureInfo.DisplayName} Vertical";

                if (tesseractTag.Contains("script"))
                    DisplayName = $"{cultureInfo.DisplayName} Script";

                try
                {
                    GLanguage languageData = GLanguage.GetLanguage(DisplayName);
                    return isNative ? languageData.NativeName : languageData.Name;
                }
                catch
                {
                    return DisplayName;
                }
            }
        }

        public static string GetDisplayNameFromID(int langID, bool isNative)
        {
            string tesseractTag = GetTesseractTagFromID(langID);
            return GetDisplayNameFromTesseractTag(tesseractTag, isNative);
        }

        public static dynamic GetTranslatorService(int providerID)
        {
            switch (providerID)
            {
                case 1:
                    return new GoogleTranslator2();
                case 2:
                    return new BingTranslator();
                case 3:
                    return new MicrosoftTranslator();
                case 4:
                    return new YandexTranslator();
                default:
                    return new GoogleTranslator();
            }
        }

        public static async Task<string> TranslatedText(string text, int targetLanguage)
        {
            // Translated text
            var translator = LanguageList.GetTranslatorService(App.setting.TranslationProvider);
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
