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
}

public class TesseractHelper
{
    private static readonly string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    public static string GetTessdataPath(int accID)
    {
        if (accID == 0)
            return Path.Combine(appData, "ScreenLookup", "tessdata_fast");
        if (accID == 1)
            return Path.Combine(appData, "ScreenLookup", "tessdata");
        return Path.Combine(appData, "ScreenLookup", "tessdata_best");
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
    }
}

internal class HunspellHelper
{
    private static readonly string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    public static string FilePath = Path.Combine(appData, "ScreenLookup", "hunspell");

    public static Dictionary<string, string> FileNames = new Dictionary<string, string> {
        {"Afrikaans", "af_ZA/af_ZA"},
        {"Arabic", "ar/ar"},
        {"Belarusian", "be_BY/be-official"},
        {"Bulgarian", "bg_BG/bg_BG"},
        {"Bengali", "bn_BD/bn_BD"},
        {"Tibetan", "bo/bo"},
        {"Bosnian", "bs_BA/bs_BA"},
        {"Czech", "cs_CZ/cs_CZ"},
        {"Danish", "da_DK/da_DK"},
        {"German", "de/de_DE_frami"},
        {"Greek", "el_GR/el_GR"},
        {"English", "en/en_US"},
        {"Esperanto", "eo/eo"},
        {"Estonian", "et_EE/et_EE"},
        {"Persian", "fa_IR/fa-IR"},
        {"French", "fr_FR/fr"},
        {"Gaelic", "gd_GB/gd_GB"},
        {"Galician", "gl/gl_ES"},
        {"Gujarati", "gu_IN/gu_IN"},
        {"Hebrew", "he_IL/he_IL"},
        {"Hindi", "hi_IN/hi_IN"},
        {"Croatian", "hr_HR/hr_HR"},
        {"Hungarian", "hu_HU/hu_HU"},
        {"Indonesian", "id/id_ID"},
        {"Icelandic", "is/is"},
        {"Italian", "it_IT/it_IT"},
        {"Korean", "ko_KR/ko_KR"},
        {"Lao", "lo_LA/lo_LA"},
        {"Lithuanian", "lt_LT/lt"},
        {"Latvian", "lv_LV/lv_LV"},
        {"Mongolian", "mn_MN/mn_MN"},
        {"Nepali", "ne_NP/ne_NP"},
        {"Dutch", "nl_NL/nl_NL"},
        {"Norwegian", "no/nb_NO"},
        {"Polish", "pl_PL/pl_PL"},
        {"Portuguese", "pt_BR/pt_BR"},
        {"Romanian", "ro/ro_RO"},
        {"Russian", "ru_RU/ru_RU"},
        {"Slovak", "sk_SK/sk_SK"},
        {"Slovenian", "sl_SI/sl_SI"},
        {"Albanian", "sq_AL/sq_AL"},
        {"Serbian", "sr/sr"},
        {"Swedish", "sv_SE/sv_FI"},
        {"Swahili", "sw_TZ/sw_TZ"},
        {"Telugu", "te_IN/te_IN"},
        {"Thai", "th_TH/th_TH"},
        {"Turkish", "tr_TR/tr_TR"},
        {"Ukrainian", "uk_UA/uk_UA"},
        {"Vietnamese", "vi/vi_VN"},
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
}