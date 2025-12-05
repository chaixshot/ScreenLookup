using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ScreenLookup.src.models
{
    internal class LangugeList
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

        public static string GetTesseractTagFromName(string textName)
        {
            string tesseractTag = textName.Split('.').First();

            return tesseractTag;
        }

        public static string GetTesseractTagFromID(int langID)
        {
            string tesseractTag = GetTesseractTagFromName(LangugeList.LanguageTesseract[langID]);

            return tesseractTag;
        }

        public static string GetLanguageShortageFromID(int langID)
        {
            string tesseractTag = LangugeList.GetTesseractTagFromID(langID);
            string shorted = tesseractTag.Substring(0, 2);

            return shorted;
        }

        public static string CultureDisplayName(string tesseractTag)
        {
            string tessLangTag = tesseractTag.Replace("_frak", "");
            tessLangTag = tessLangTag.Replace("_old", "");
            tessLangTag = tessLangTag.Replace("_latn", "");
            tessLangTag = tessLangTag.Replace("_vert", "");

            CultureInfo cultureInfo = tessLangTag switch
            {
                "chi_sim" => new CultureInfo("zh-Hans"),
                "chi_tra" => new CultureInfo("zh-Hant"),
                _ => new CultureInfo(tessLangTag)
            };
 
            if (tesseractTag == "dan_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (tesseractTag == "deu_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (tesseractTag == "ita_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (tesseractTag == "kat_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (tesseractTag == "slk_frak")
                return $"{cultureInfo.DisplayName} (Fraktur)";

            if (tesseractTag == "spa_old")
                return $"{cultureInfo.DisplayName} (Old)";

            if (tesseractTag == "srp_latn")
                return $"{cultureInfo.DisplayName} (Latin)";

            if (tesseractTag.Contains("vert"))
                return $"{cultureInfo.DisplayName} Vertical";

            if (tesseractTag.Contains("script"))
                return $"{cultureInfo.DisplayName} Script";

            return $"{cultureInfo.DisplayName}";
        }
    }
}
