using WeCantSpell.Hunspell;

namespace ScreenLookup.src.utils
{
    internal static class HunspellHelper
    {
        public static readonly string FilePath = Path.Combine(App.appDataFolder, "hunspell");
        public static WordList? HunspellEngine { get; private set; }

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
            if (langID < 0 || langID >= LanguageList.LangList.Length) return false;

            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            if (LangList.TryGetValue(tessTag, out string? fileName))
            {
                string nameTag = fileName.Contains('/') ? fileName.Split('/')[1] : fileName;
                string affPath = Path.Combine(FilePath, $"{nameTag}.aff");
                string dicPath = Path.Combine(FilePath, $"{nameTag}.dic");

                return File.Exists(affPath) && File.Exists(dicPath);
            }

            return false;
        }

        public static async Task<bool> InstallModel(int langID)
        {
            if (langID < 0 || langID >= LanguageList.LangList.Length) return false;
            if (IsInstalled(langID)) return true;

            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            if (!LangList.TryGetValue(tessTag, out string? fileName))
            {
                SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" doesn't support Hunspell", SnackbarType.Error);
                return false;
            }

            SnackbarHost.Show("Hunspell", $"Downloading Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}...", SnackbarType.Info, timeout: 99999, closeButton: false);

            foreach (string extension in new[] { "aff", "dic" })
            {
                string nameTag = fileName.Contains('/') ? fileName.Split('/')[1] : fileName;
                string zipPath = Path.Combine(App.tempFolder, $"{nameTag}.{extension}.zip");
                bool isFileExist = File.Exists(Path.Combine(FilePath, $"{nameTag}.{extension}"));
                bool isDownloaded = false;

                if (!isFileExist)
                {
                    DownloadHelper fileDownloader = new();
                    isDownloaded = await fileDownloader.DownloadFileAsync($"https://translator.gres.biz/resources/dictionaries/{fileName}.{extension}.zip", zipPath);
                }

                if (isDownloaded)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, App.tempFolder, null, true);
                    FileInfo zipFile = new(zipPath);
                    try { if (zipFile.Exists) zipFile.Delete(); } catch { }

                    await DownloadHelper.MoveFileToFolder(Path.Combine(App.tempFolder, $"{nameTag}.{extension}"), FilePath);
                }
                else if (!isFileExist)
                {
                    SnackbarHost.Show("Hunspell", $"Unable to download \"Hunspell - {LanguageList.GetDisplayNameFromID(langID, true)}\"", SnackbarType.Error);
                    return false;
                }
            }

            return true;
        }

        public static void CreateHunspellEngine(int langID)
        {
            string tessTag = LanguageList.GetTesseractTagFromID(langID);

            HunspellEngine = null;

            if (LangList.TryGetValue(tessTag, out string? fileName))
            {
                string nameTag = fileName.Split('/')[1];
                string dicPath = Path.Combine(FilePath, $"{nameTag}.dic");
                string affPath = Path.Combine(FilePath, $"{nameTag}.aff");

                if (File.Exists(affPath) && File.Exists(dicPath))
                    HunspellEngine = WordList.CreateFromFiles(dicPath, affPath);
            }
            else
            {
                SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" doesn't support Hunspell", SnackbarType.Error);
            }
        }

        public static void RemoveHunspellEngine()
        {
            HunspellEngine = null;
        }

        public static string CorrectionWord(string text)
        {
            if (!string.IsNullOrEmpty(text) && HunspellEngine?.Check(text) == false)
            {
                IEnumerable<string> suggestions = HunspellEngine.Suggest(text);
                string? firstSuggestion = suggestions.FirstOrDefault();

                if (firstSuggestion != null)
                    return firstSuggestion;
            }

            return text;
        }

        public static string CorrectionParagraph(string text)
        {
            if (string.IsNullOrEmpty(text) || HunspellEngine == null)
                return text;

            return System.Text.RegularExpressions.Regex.Replace(text, @"\b\p{L}+\b", match => CorrectionWord(match.Value));
        }
    }
}
