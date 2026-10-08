using System.IO;

namespace ScreenLookup.src.utils
{
    public static class TesseractHelper
    {
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

        public static string GetTessdataPath(int accID)
        {
            if (accID == 0)
                return Path.Combine(App.appDataFolder, "tessdata_fast");
            if (accID == 1)
                return Path.Combine(App.appDataFolder, "tessdata");
            return Path.Combine(App.appDataFolder, "tessdata_best");
        }

        public static bool IsInstalled(int accID, int langID)
        {
            return App.setting.LoadedTesseract.ContainsKey($"{accID},{langID}");
        }

        public static void SaveInstalled(int accID, int langID)
        {
            App.setting.LoadedTesseract.Add($"{accID},{langID}", true);
            App.setting.Save();

            App.captureWindow.LoadInstalledLanguage();
            App.captureWindow.CreateTesseractEngine();
        }
    }
}
