using System.CommandLine;
using RestSharp;
using System.Threading.Tasks;

class LucidExporter
{

    static void Main(string[] args)
    {
        try
        {
            var documentsFileOption = new Option<FileInfo?>(name: "-f", description: "A file containing a lucid document id on each line.");
            var documentIdOption = new Option<string?>(name: "-d", description: "The document id (guid from url) of the lucid chart document you want to export.  Can be a comma delimited list.");
            var outputFolderOption = new Option<DirectoryInfo?>(name: "-o", description: "The location to export images to.", getDefaultValue: () => new DirectoryInfo("./LucidExports"));

            var rootCommand = new RootCommand("LucidChart image exporter.");
            rootCommand.AddOption(documentsFileOption);
            rootCommand.AddOption(documentIdOption);
            rootCommand.AddOption(outputFolderOption);

            rootCommand.SetHandler((documentsFile, documentId, outputFolder) =>
            {
                if (documentsFile != null)
                {
                    ProcessFile(documentsFile, outputFolder!);
                }
                else if (!string.IsNullOrWhiteSpace(documentId))
                {
                    ProcessDocument(documentId!, outputFolder!);
                }
            }, documentsFileOption, documentIdOption, outputFolderOption);

            rootCommand.Invoke(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static void ProcessFile(FileInfo inputPath, DirectoryInfo outputFolder)
    {
        Console.WriteLine($"reading {inputPath}");

        foreach (var line in File.ReadAllLines(inputPath.FullName))
        {
            var parts = line.Split("#"); // allow for comments in document after #
            ProcessDocument(parts[0], outputFolder);
        }
    }

    private static void ProcessDocument(string documentID, DirectoryInfo outputFolder)
    {
        string apiKey = GetAPIKey();
        ProcessDocument(apiKey, documentID, outputFolder, "image/png;dpi=256", "png", "content");
    }

    private static string GetAPIKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("LUCID_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new ApplicationException("Error: API key not found. Please set the LUCID_API_KEY environment variable.");

        return apiKey;
    }

    static void ProcessDocument(string apiKey, string documentID, DirectoryInfo outputFolder, string contentType, string exportType, string cropSize)
    {
        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder.FullName);

        try
        {
            // Get document metadata
            var documentMeta = GetDocumentMetadata(documentID, apiKey);
            if (documentMeta == null)
            {
                Console.WriteLine($"Failed to fetch metadata for document {documentID}");
                return;
            }

            string documentFolder = Path.Combine(outputFolder.FullName, (string)documentMeta.title);
            Directory.CreateDirectory(documentFolder);

            var pageCount = (int)documentMeta.pages.Count;
            Console.WriteLine($"Processing document {documentID} - {documentMeta.title} - {pageCount} pages to export...");

            Parallel.For(0, pageCount, (Action<int>)(pageIndex =>
            {
                var pageName = documentMeta.pages[pageIndex].title;
                string outputFile = Path.Combine(documentFolder, $"[{(pageIndex + 1).ToString("D2")}] - {pageName}.{exportType.ToLower()}");

                // Lucid API page indexing begins at 1 and not 0.
                string pageURL = $"https://api.lucid.co/documents/{documentID}?page={pageIndex + 1}&crop={cropSize}";

                var (success, errorMessage) = DownloadImageToFile(apiKey, pageURL, outputFile, contentType);

                if (success)
                    Console.WriteLine($"Exported {outputFile}");
                else
                    Console.WriteLine($"Failed to export page {pageName} for document {documentID} - {errorMessage}");
            }));

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing document {documentID}: {ex.Message}");
            throw;
        }
    }

    // Get document metadata
    static dynamic? GetDocumentMetadata(string guid, string apiKey)
    {
        string url = $"https://api.lucid.co/documents/{guid}/contents";
        var client = new RestClient(url);
        RestRequest request = CreateRestRequest(apiKey);

        var response = client.Get(request);
        if (response is null || !response.IsSuccessful || response.Content == null) return null;

        return Newtonsoft.Json.JsonConvert.DeserializeObject(response.Content);
    }

    // Download page as an image
    static (bool success, string err) DownloadImageToFile(string apiKey, string url, string fileName, string contentType)
    {
        var client = new RestClient(url);
        RestRequest request = CreateRestRequest(apiKey);
        request.AddHeader("accept", $"{contentType}");

        try
        {
            var response = client.DownloadData(request);

            if (response is null || response.Length == 0)
                return (false, "No response returned.");

            File.WriteAllBytes(fileName, response);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to download image: {ex.Message}");
        }
    }

    private static RestRequest CreateRestRequest(string apiKey)
    {
        var request = new RestRequest("", Method.Get);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Lucid-Api-Version", "1");
        request.AddHeader("authorization", $"Bearer {apiKey}");
        return request;
    }
}
