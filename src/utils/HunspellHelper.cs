using System.IO;
using System.Net.Http;

class HunspellHelper
{
    private static string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    public static string FilePath = $"{appData}\\ScreenLookup\\hunspell";

    public static Dictionary<string, string> FileNames = new Dictionary<string, string> {
        {"Afrikaans", "af_ZA.af_ZA"},
        {"Arabic", "ar.ar"},
        {"Belarusian", "be_BY.be-official"},
        {"Bulgarian", "bg_BG.bg_BG"},
        {"Bengali", "bn_BD.bn_BD"},
        {"Tibetan", "bo.bo"},
        {"Bosnian", "bs_BA.bs_BA"},
        {"Czech", "cs_CZ.cs_CZ"},
        {"Danish", "da_DK.da_DK"},
        {"German", "de.de_DE_frami"},
        {"Greek", "el_GR.el_GR"},
        {"English", "en.en_US"},
        {"Esperanto", "eo.eo"},
        {"Estonian", "et_EE.et_EE"},
        {"Persian", "fa_IR.fa-IR"},
        {"French", "fr_FR.fr"},
        {"Gaelic", "gd_GB.gd_GB"},
        {"Galician", "gl.gl_ES"},
        {"Gujarati", "gu_IN.gu_IN"},
        {"Hebrew", "he_IL.he_IL"},
        {"Hindi", "hi_IN.hi_IN"},
        {"Croatian", "hr_HR.hr_HR"},
        {"Hungarian", "hu_HU.hu_HU"},
        {"Indonesian", "id.id_ID"},
        {"Icelandic", "is.is"},
        {"Italian", "it_IT.it_IT"},
        {"Korean", "ko_KR.ko_KR"},
        {"Lao", "lo_LA.lo_LA"},
        {"Lithuanian", "lt_LT.lt"},
        {"Latvian", "lv_LV.lv_LV"},
        {"Mongolian", "mn_MN.mn_MN"},
        {"Nepali", "ne_NP.ne_NP"},
        {"Dutch", "nl_NL.nl_NL"},
        {"Norwegian", "no.nb_NO"},
        {"Polish", "pl_PL.pl_PL"},
        {"Portuguese", "pt_BR.pt_BR"},
        {"Romanian", "ro.ro_RO"},
        {"Russian", "ru_RU.ru_RU"},
        {"Slovak", "sk_SK.sk_SK"},
        {"Slovenian", "sl_SI.sl_SI"},
        {"Albanian", "sq_AL.sq_AL"},
        {"Serbian", "sr.sr"},
        {"Swedish", "sv_SE.sv_FI"},
        {"Swahili", "sw_TZ.sw_TZ"},
        {"Telugu", "te_IN.te_IN"},
        {"Thai", "th_TH.th_TH"},
        {"Turkish", "tr_TR.tr_TR"},
        {"Ukrainian", "uk_UA.uk_UA"},
        {"Vietnamese", "vi.vi_VN"},
    };
}

public class HunspellFileDownloader
{
    private readonly HttpClient _client;

    public HunspellFileDownloader()
    {
        _client = new HttpClient();
        // It's a good practice to set a user-agent when making requests
        _client.DefaultRequestHeaders.Add("User-Agent", "Text Grab settings language downloader");
    }

    public async Task DownloadFileAsync(string filenameToDownload, string localDestination)
    {
        string fileUrl = $"https://translator.gres.biz/resources/dictionaries/{filenameToDownload}";
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}