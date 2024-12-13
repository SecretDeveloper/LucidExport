using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using RestSharp;

class LucidExporter
{
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length < 2 || (args[0] != "-d" && args[0] != "-g"))
            {
                PrintUsage();
                return;
            }

            switch (args[0])
            {
                case "-d":
                    string documentPath = args[1];
                    if (!File.Exists(documentPath))
                    {
                        Console.WriteLine($"Error: The file at path '{documentPath}' does not exist.");
                        return;
                    }
                    Console.WriteLine($"Processing document at: {documentPath}");
                    await ProcessDocument(documentPath);
                    break;

                case "-g":
                    string guidsInput = args[1];
                    string[] guids = guidsInput.Split(',');
                    if (guids.Length == 0)
                    {
                        Console.WriteLine("Error: Invalid GUIDs provided. Ensure they are valid and comma-separated.");
                        return;
                    }
                    Console.WriteLine("Processing GUIDs:");
                    foreach (var guid in guids)
                    {
                        await ProcessGuid(guid);
                    }
                    break;

                default:
                    PrintUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: LucidExport -d <documentpath> | -g <guids>");
        Console.WriteLine("Examples:");
        Console.WriteLine("  LucidExport -d 'C:\\path\\to\\document.txt'");
        Console.WriteLine("  LucidExport -g 'guid1, guid2, guid3'");
    }

    static async Task ProcessDocument(string documentPath)
    {
        foreach (var line in File.ReadAllLines(documentPath))
        {
            var parts = line.Split("#"); // allow for comments in document after #
            await ProcessGuid(parts[0]);
        }
    }

    static async Task ProcessGuid(string guid)
    {
        var contentType = "image/png;dpi=256"; // Default export type
        var exportType = "png";
        var cropSize = "content"; // Default crop size

        // Read API key from environment variable
        string apiKey = Environment.GetEnvironmentVariable("LUCID_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key not found. Please set the LUCID_API_KEY environment variable.");
            return;
        }

        string outputFolder = "ExportedPages";
        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder);

        try
        {
            // Get document metadata
            var metadata = await GetDocumentMetadata(guid, apiKey);
            if (metadata == null)
            {
                Console.WriteLine($"Failed to fetch metadata for document {guid}");
                return;
            }

            string documentName = metadata.title;
            string documentFolder = outputFolder + "\\" + documentName;
            Directory.CreateDirectory(documentFolder);

            Console.WriteLine($"Processing document {guid}, {metadata.pageCount} pages to export...");

            var pageCount = int.Parse(metadata.pageCount.ToString());
            // Iterate over pages
            for (int p = 1; p <= pageCount; p++)
            {
                var pageNumber = p;
                string exportUrl = $"https://api.lucid.co/documents/{guid}?page={pageNumber}&crop={cropSize}";

                // Export page
                string fileName = Path.Combine(documentFolder, $"{documentName} - Page{pageNumber}.{exportType.ToLower()}");
                var success = await DownloadPageImage(exportUrl, fileName, apiKey, contentType);

                if (success)
                    Console.WriteLine($"Exported page {pageNumber} to {fileName}");
                else
                    Console.WriteLine($"Failed to export page {pageNumber} for document {guid}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing document {guid}: {ex.Message}");
            throw;
        }
    }

    // Get document metadata
    static async Task<dynamic> GetDocumentMetadata(string guid, string apiKey)
    {
        string url = $"https://api.lucid.co/documents/{guid}";
        var client = new RestClient(url);
        var request = new RestRequest("", Method.Get);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Lucid-Api-Version", "1");
        request.AddHeader("authorization", $"Bearer {apiKey}");

        try
        {
            var response = await client.GetAsync(request);
            if (response != null && response.IsSuccessful)
            {
                if (response.Content == null) throw new ApplicationException($"Failed to get document metadata for {guid}");

                return Newtonsoft.Json.JsonConvert.DeserializeObject(response.Content);

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch metadata: {ex.Message}");
        }

        return null;
    }

    // Download page as an image
    static async Task<bool> DownloadPageImage(string url, string fileName, string apiKey, string contentType)
    {
        var client = new RestClient(url);
        var request = new RestRequest("", Method.Get);
        request.AddHeader("authorization", $"Bearer {apiKey}");
        request.AddHeader("Lucid-Api-Version", "1");
        request.AddHeader("accept", $"{contentType}");

        try
        {
            var response = await client.DownloadDataAsync(request);

            if (response != null && response.Length > 0)
            {
                await File.WriteAllBytesAsync(fileName, response);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download image: {ex.Message}");
        }

        return false;
    }
}
