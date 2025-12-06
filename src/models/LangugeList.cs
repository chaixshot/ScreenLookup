using GTranslate.Translators;
using HunspellSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using GLanguage = GTranslate.Language;

namespace ScreenLookup.src.models
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
            string tesseractTag = LanguageList.GetTesseractTagFromID(langID);
            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tesseractTag);
                return languageData.ISO6391;
            }
            catch
            {
                return tesseractTag.Substring(0, 2);
            }
        }

        public static string GetLanguageISO6393FromID(int langID)
        {
            string tesseractTag = LanguageList.GetTesseractTagFromID(langID);
            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tesseractTag);
                return languageData.ISO6393;
            }
            catch
            {
                return tesseractTag.Substring(0, 3);
            }
        }

        public static string CultureDisplayNameFromTesseractTag(string tesseractTag)
        {
            string tessLangTag = tesseractTag.Replace("_frak", "");
            tessLangTag = tessLangTag.Replace("_old", "");
            tessLangTag = tessLangTag.Replace("_latn", "");
            tessLangTag = tessLangTag.Replace("_vert", "");

            try
            {
                GLanguage languageData = GLanguage.GetLanguage(tessLangTag);
                return languageData.NativeName;
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
                    return languageData.NativeName;
                }
                catch
                {
                    return DisplayName;
                }
            }
        }

        public static string CultureDisplayNameFromID(int langID)
        {
            string tesseractTag = GetTesseractTagFromID(langID);
            return CultureDisplayNameFromTesseractTag(tesseractTag);
        }

        public static dynamic GetTranslatorService()
        {
            switch (Setting.TranslationProvider)
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
    }
}
