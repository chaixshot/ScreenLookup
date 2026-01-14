using HunspellSharp;
using Microsoft.Win32;
using ScreenLookup;
using ScreenLookup.src.utils;
using System.IO;
using System.Net.Http;

class DownloadHelper
{
    private readonly HttpClient _client;

    public static async Task MoveFileToFolder(string sourcePath, string destinationPath)
    {
        System.IO.Directory.CreateDirectory(destinationPath);
        FileInfo file = new(sourcePath);
        string filePath = $@"{destinationPath}\{file.Name}";

        if (File.Exists(filePath))
        {
            FileInfo oldFile = new(filePath);
            oldFile.Delete();
        }
        file.MoveTo(filePath);
    }

    public DownloadHelper()
    {
        _client = new HttpClient();
        // It's a good practice to set a user-agent when making requests
        _client.DefaultRequestHeaders.Add("User-Agent", "ScreenLookup tesseract language downloader");
    }

    public async Task<bool> DownloadFileAsync(string fileUrl, string localDestination)
    {
        try
        {
            // Send a GET request to the specified URL
            HttpResponseMessage response = await _client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();

            // Read the response content
            byte[] fileContents = await response.Content.ReadAsByteArrayAsync();

            // Write the content to a file on the local file system
            await File.WriteAllBytesAsync(localDestination, fileContents);
            Console.WriteLine("File downloaded successfully.");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");

            return false;
        }
    }

    public static void DeleteDownloadedAppData()
    {
        foreach (string folderName in new string[] { "hunspell", "tessdata", "tessdata_best", "tessdata_fast" })
        {
            DirectoryInfo Folder = new(Path.Combine(App.appDataFolder, folderName));
            if (Folder.Exists)
                Folder.Delete(true);
        }
    }
}

public class TesseractHelper
{
    public static readonly string[] LangList = [
        "afr",
        "amh",
        "ara",
        "asm",
        "aze",
        "aze_cyrl",
        "bel",
        "ben",
        "bod",
        "bos",
        "bre",
        "bul",
        "cat",
        "ceb",
        "ces",
        "chi_sim",
        "chi_sim_vert",
        "chi_tra",
        "chi_tra_vert",
        "chr",
        "cos",
        "cym",
        "dan",
        "dan_frak",
        "deu",
        "deu_frak",
        "div",
        "dzo",
        "ell",
        "eng",
        "enm",
        "epo",
        "equ",
        "est",
        "eus",
        "fao",
        "fas",
        "fil",
        "fin",
        "fra",
        "frk",
        "frm",
        "fry",
        "gla",
        "gle",
        "glg",
        "grc",
        "guj",
        "hat",
        "heb",
        "hin",
        "hrv",
        "hun",
        "hye",
        "iku",
        "ind",
        "isl",
        "ita",
        "ita_old",
        "jav",
        "jpn",
        "jpn_vert",
        "kan",
        "kat",
        "kat_old",
        "kaz",
        "khm",
        "kir",
        "kmr",
        "kor",
        "kor_vert",
        "lao",
        "lat",
        "lav",
        "lit",
        "ltz",
        "mal",
        "mar",
        "mkd",
        "mlt",
        "mon",
        "mri",
        "msa",
        "mya",
        "nep",
        "nld",
        "nor",
        "oci",
        "ori",
        "osd",
        "pan",
        "pol",
        "por",
        "pus",
        "que",
        "ron",
        "rus",
        "san",
        "sin",
        "slk",
        "slk_frak",
        "slv",
        "snd",
        "spa",
        "spa_old",
        "sqi",
        "srp",
        "srp_latn",
        "sun",
        "swa",
        "swe",
        "syr",
        "tam",
        "tat",
        "tel",
        "tgk",
        "tgl",
        "tha",
        "tir",
        "ton",
        "tur",
        "uig",
        "ukr",
        "urd",
        "uzb",
        "uzb_cyrl",
        "vie",
        "yid",
        "yor",
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
        RegistryKey key = App.setting.RegLoadedTesseract.CreateSubKey(accID.ToString());
        var reg = key.GetValue(LanguageList.GetTesseractTagFromID(langID));

        return reg != null;
    }

    public static void SaveInstalled(int accID, int langID)
    {
        RegistryKey key = App.setting.RegLoadedTesseract.CreateSubKey(accID.ToString());
        key.SetValue(LanguageList.GetTesseractTagFromID(langID), true);

        App.captureWindow.LoadInstalledLanguage();
        App.captureWindow.CreateTesseractEngine();
    }
}

internal class HunspellHelper
{
    public static string FilePath = Path.Combine(App.appDataFolder, "hunspell");
    public static Hunspell HunspellEngine;

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

    public static bool IsInstalled(int langID)
    {
        object reg = App.setting.RegLoadedHunspell.GetValue(langID.ToString());
        bool isInstalled = reg != null;

        return isInstalled;
    }

    public static void SaveInstalled(int langID)
    {
        App.setting.RegLoadedHunspell.SetValue(langID.ToString(), true);
    }

    public static void CreateHunspellEngine(int langID)
    {
        string tessTag = LanguageList.GetTesseractTagFromID(langID);

        HunspellEngine?.Dispose();
        if (LangList.TryGetValue(tessTag, out string? fileName))
        {
            string nameTag = fileName.Split('/')[1];
            HunspellEngine = new Hunspell($"{FilePath}\\{nameTag}.aff", $"{FilePath}\\{nameTag}.dic");
        }
        else
            SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(langID, true)}\" dosen't support Hunspell", "error");
    }

    public static string CorrectionWord(string word)
    {
        if (!HunspellEngine.Spell(word))
        {
            List<string> suggestions = HunspellEngine.Suggest(word);
            if (suggestions.Count != 0)
                word = suggestions[0];
        }

        return word;
    }
}