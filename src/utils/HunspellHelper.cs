using HunspellSharp;
using ScreenLookup.src.utils.Database;
using System.IO;

namespace ScreenLookup.src.utils
{
    internal static class HunspellHelper
    {
        public static readonly string FilePath = Path.Combine(App.appDataFolder, "hunspell");
        public static Hunspell? HunspellEngine { get; private set; }

        public static readonly Dictionary<string, string> LangList = new()
        {
            {"afr", "af_ZA/af_ZA"},
            {"ara", "ar/ar"},
            {"bel", "be_BY/be-official"},
            {"bul", "bg_BG/bg_BG"},
            {"ben", "bn_BD/bn_BD"},
            {"bod", "bo/bo"},
            {"bos", "bs_BA/bs_BA"},
            {"ces", "cs_CZ/cs_CZ"},
            {"dan", "da_DK/da_DK"},
            {"deu", "de/de_DE_frami"},
            {"ell", "el_GR/el_GR"},
            {"eng", "en/en_US"},
            {"epo", "eo/eo"},
            {"est", "et_EE/et_EE"},
            {"fas", "fa_IR/fa-IR"},
            {"fra", "fr_FR/fr"},
            {"gla", "gd_GB/gd_GB"},
            {"glg", "gl/gl_ES"},
            {"guj", "gu_IN/gu_IN"},
            {"heb", "he_IL/he_IL"},
            {"hin", "hi_IN/hi_IN"},
            {"hrv", "hr_HR/hr_HR"},
            {"hun", "hu_HU/hu_HU"},
            {"ind", "id/id_ID"},
            {"isl", "is/is"},
            {"ita", "it_IT/it_IT"},
            {"kor", "ko_KR/ko_KR"},
            {"lao", "lo_LA/lo_LA"},
            {"lit", "lt_LT/lt"},
            {"lat", "lv_LV/lv_LV"},
            {"mon", "mn_MN/mn_MN"},
            {"nep", "ne_NP/ne_NP"},
            {"nld", "nl_NL/nl_NL"},
            {"nor", "no/nb_NO"},
            {"pol", "pl_PL/pl_PL"},
            {"por", "pt_BR/pt_BR"},
            {"ron", "ro/ro_RO"},
            {"rus", "ru_RU/ru_RU"},
            {"slk", "sk_SK/sk_SK"},
            {"slv", "sl_SI/sl_SI"},
            {"sqi", "sq_AL/sq_AL"},
            {"srp", "sr/sr"},
            {"swe", "sv_SE/sv_FI"},
            {"swa", "sw_TZ/sw_TZ"},
            {"tel", "te_IN/te_IN"},
            {"tha", "th_TH/th_TH"},
            {"tur", "tr_TR/tr_TR"},
            {"ukr", "uk_UA/uk_UA"},
            {"vie", "vi/vi_VN"},
        };

        static HunspellHelper()
        {
            CreateHunspellEngine(App.setting.SourceLanguage);
        }

        public static bool IsInstalled(int langID)
        {
            return App.setting.LoadedHunspell.ContainsKey(langID.ToString());
        }

        public static void SaveInstalled(int langID)
        {
            App.setting.LoadedHunspell.Add(langID.ToString(), true);
            App.setting.Save();
        }

        public static void CreateHunspellEngine(int langID)
        {
            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            HunspellEngine?.Dispose();
            HunspellEngine = null;

            if (LangList.TryGetValue(tessTag, out string? fileName))
            {
                string nameTag = fileName.Split('/')[1];
                string affPath = Path.Combine(FilePath, $"{nameTag}.aff");
                string dicPath = Path.Combine(FilePath, $"{nameTag}.dic");

                if (File.Exists(affPath) && File.Exists(dicPath))
                {
                    HunspellEngine = new Hunspell(affPath, dicPath);
                }
            }
            else
            {
                SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" doesn't support Hunspell", SnackbarType.Error);
            }
        }

        public static void RemoveHunspellEngine()
        {
            HunspellEngine?.Dispose();
            HunspellEngine = null;
        }

        public static string CorrectionWord(string word)
        {
            if (HunspellEngine != null && !HunspellEngine.Spell(word))
            {
                List<string> suggestions = HunspellEngine.Suggest(word);
                if (suggestions.Count != 0)
                    word = suggestions[0];
            }

            return word;
        }
    }
}
