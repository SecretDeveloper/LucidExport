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
        // Validate input
        if (args.Length <1)
        {
            Console.WriteLine("Usage: LucidExport <documentIds,>");
            Console.WriteLine("Example: lucid-exporter \"guid,guid,guid\"");
            return;
        }

        // Input parameters
        var documentIds = args[0].Split(',').Select(id => id.Trim()).ToArray();
        var contentType = "image/png;dpi=256"; // Default export type
        var exportType = "png";
        var cropSize = "content"; // Default crop size

         // Read API key from environment variable
        string apiKey = Environment.GetEnvironmentVariable("LUCID_API_KEY")??"";
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key not found. Please set the LUCID_API_KEY environment variable.");
            return;
        }

        string outputFolder = "ExportedPages";
        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder);

        foreach (var documentId in documentIds)
        {
            try
            {
                // Get document metadata
                var metadata = await GetDocumentMetadata(documentId, apiKey);

                if (metadata == null)
                {
                    Console.WriteLine($"Failed to fetch metadata for document {documentId}");
                    continue;
                }
                var documentName = metadata.title;
                var documentFolder = outputFolder+"\\"+documentName;
                Directory.CreateDirectory(documentFolder);

                Console.WriteLine($"Processing document {documentId}, {metadata.pageCount} pages to export...");

                var pageCount = int.Parse(metadata.pageCount.ToString());
                // Iterate over pages
                for(var p = 1; p <= pageCount; p++)
                {
                    var pageNumber = p;
                    string exportUrl = $"https://api.lucid.co/documents/{documentId}?page={pageNumber}&crop={cropSize}";

                    // Export page
                    var fileName = Path.Combine(documentFolder, $"{documentName} - Page{pageNumber}.{exportType.ToLower()}");
                    var success = await DownloadPageImage(exportUrl, fileName, apiKey, contentType);

                    if (success)
                        Console.WriteLine($"Exported page {pageNumber} to {fileName}");
                    else
                        Console.WriteLine($"Failed to export page {pageNumber} for document {documentId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing document {documentId}: {ex.Message}");
                throw;
            }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error line {ex}");
      }
    }

    // Get document metadata
    static async Task<dynamic> GetDocumentMetadata(string documentId, string apiKey)
    {
        string url = $"https://api.lucid.co/documents/{documentId}";
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
              if(response.Content == null) throw new ApplicationException($"Failed to get document metadata for {documentId}");

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
