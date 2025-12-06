using GTranslate;
using ScreenLookup.src.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

internal class TesseractHelper
{
    public static string GetTessdataPath()
    {
        var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        int accID = Setting.SourceLanguageAccuracy;
        if (accID == 0)
            return $"{appData}\\ScreenLookup\\tessdata_fast";
        if (accID == 1)
            return $"{appData}\\ScreenLookup\\tessdata";
        return $"{appData}\\ScreenLookup\\tessdata_best";
    }
}

public class TesseractGitHubFileDownloader
{
    private readonly HttpClient _client;

    public TesseractGitHubFileDownloader()
    {
        _client = new HttpClient();
        // It's a good practice to set a user-agent when making requests
        _client.DefaultRequestHeaders.Add("User-Agent", "Text Grab settings language downloader");
    }

    public async Task DownloadFileAsync(string filenameToDownload, string localDestination)
    {
        int accID = Setting.SourceLanguageAccuracy;
        string fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/{filenameToDownload}";
        if (accID == 0)
            fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}";
        if (accID == 1)
            fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata_best/main/{filenameToDownload}";

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