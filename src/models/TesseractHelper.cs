using GTranslate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

internal class TesseractHelper
{
    
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
        // Construct the URL to the raw content of the file in the GitHub repository
        // https://github.com/tesseract-ocr/tessdata
        string fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}";

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