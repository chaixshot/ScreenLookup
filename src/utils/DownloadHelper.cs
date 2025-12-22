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
    public static Dictionary<int, Hunspell> HunspellEngine = [];

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
        var reg = App.setting.RegLoadedHunspell.GetValue(langID.ToString());

        return reg != null;
    }

    public static void SaveInstalled(int langID)
    {
        App.setting.RegLoadedHunspell.SetValue(langID.ToString(), true);
    }

    public static string CorrectionWord(string word, int sourceLanguage)
    {
        string tessTag = LanguageList.GetTesseractTagFromID(sourceLanguage);
        if (LangList.TryGetValue(tessTag, out string? fileName))
        {
            string nameTag = fileName.Split('/')[1];

            if (!HunspellEngine.TryGetValue(sourceLanguage, out Hunspell hunspell))
            {
                hunspell = new Hunspell($"{FilePath}\\{nameTag}.aff", $"{FilePath}\\{nameTag}.dic");
                HunspellEngine.TryAdd(sourceLanguage, hunspell);
            }

            if (!hunspell.Spell(word))
            {
                List<string> suggestions = hunspell.Suggest(word);
                if (suggestions.Count != 0)
                    word = suggestions[0];
            }
        }
        else
        {
            SnackbarHost.Show("Hunspell", $"\"{LanguageList.GetDisplayNameFromID(sourceLanguage, true)}\" dosen't support Hunspell", "error", windows: "capture");
        }

        return word;
    }
}