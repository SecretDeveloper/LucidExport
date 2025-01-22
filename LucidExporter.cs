using RestSharp;
using System.Threading.Tasks;
using System.Diagnostics;

namespace LucidExporter
{
  public class LucidExporter
  {
    // default image type to export
    private const string _ContentType = "image/png;dpi=256";
    private const string _ExportType = "png";
    private const string _CropSize = "content";

    public bool Verbose { get; set; }

    internal void Log(string output)
    {
      if (Verbose) Console.WriteLine(output);
    }

    internal void LogError(string output)
    {
      Console.Error.WriteLine(output);
    }

    private SemaphoreSlim GetSemaphore()
    {
      return new SemaphoreSlim(12); // higher than 12 and the API starts to throttle requests -- mileage may vary here - might move to a param.
    }

    internal string GetAPIKey()
    {
      return Environment.GetEnvironmentVariable("LUCID_API_KEY") ?? "";
    }

    private RestRequest CreateRestRequest(string apiKey)
    {
      var request = new RestRequest("", Method.Get);
      request.AddHeader("accept", "application/json");
      request.AddHeader("Lucid-Api-Version", "1");
      request.AddHeader("authorization", $"Bearer {apiKey}");
      return request;
    }

    internal async Task ProcessFileAsync(string apiKey, FileInfo inputPath, DirectoryInfo outputFolder)
    {
      var semaphore = GetSemaphore();
      var tasks = new List<Task>();

      Log($"reading {inputPath}");

      foreach (var line in File.ReadAllLines(inputPath.FullName))
      {
        var parts = line.Split("#"); // allow for comments in document after #
        var task = InternalProcessDocumentAsync(semaphore, apiKey, parts[0], outputFolder, _ContentType, _ExportType, _CropSize);
        tasks.Add(task);
      }

      // Wait for all tasks to complete
      await Task.WhenAll(tasks);
    }

    internal async Task ProcessDocumentAsync(string apiKey, string documentID, DirectoryInfo outputFolder)
    {
      var semaphore = GetSemaphore();
      var tasks = new List<Task>();
      var task = InternalProcessDocumentAsync(semaphore, apiKey, documentID, outputFolder, _ContentType, _ExportType, _CropSize);
      tasks.Add(task);

      // Wait for all tasks to complete
      await Task.WhenAll(tasks);
    }

    internal async Task InternalProcessDocumentAsync(SemaphoreSlim semaphore, string apiKey, string documentID, DirectoryInfo outputFolder, string contentType, string exportType, string cropSize)
    {
      try
      {
        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder.FullName);

        dynamic? documentMeta = await GetDocumentMetadataAsync(documentID, apiKey);
        if (documentMeta == null)
        {
          LogError($"Failed to fetch metadata for document {documentID}");
          return;
        }

        string documentFolder = Path.Combine(outputFolder.FullName, (string)documentMeta.title);
        Directory.CreateDirectory(documentFolder);

        var pageCount = (int)documentMeta.pages.Count;
        Log($"Processing document {documentID} - {documentMeta.title} - {pageCount} pages to export...");

        var pageDownloadTasks = new List<Task>();

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
          // Wait until the semaphore allows a task to proceed
          await semaphore.WaitAsync();
          Log($"CurrentCount:{semaphore.CurrentCount}");

          try
          {
            var pageName = documentMeta.pages[pageIndex].title;
            string outputFile = Path.Combine(documentFolder, $"[{(pageIndex + 1).ToString("D2")}] - {pageName}.{exportType.ToLower()}");

            // Lucid API page indexing begins at 1 and not 0.
            string pageURL = $"https://api.lucid.co/documents/{documentID}?page={pageIndex + 1}&crop={cropSize}";

            // Add page download task to list for parallel execution
            pageDownloadTasks.Add(DownloadImageToFileAsync(apiKey, pageURL, outputFile, contentType)
                .ContinueWith(t =>
                {
                  try
                  {
                    if (t.Result.success)
                      Log($"Exported {outputFile}");
                    else
                      LogError($"Failed to export page {pageName} for document {documentID} - {t.Result.err}");
                  }
                  finally
                  {
                    semaphore.Release(); // Release the semaphore when done
                    Log($"Release - CurrentCount:{semaphore.CurrentCount}");
                  }
                }));
          }
          catch (Exception ex)
          {
            LogError($"Error downloading page {pageIndex + 1}: {ex.Message}");
          }
        }
        // Wait for all page download tasks to complete
        await Task.WhenAll(pageDownloadTasks);
      }
      catch (Exception ex)
      {
        LogError($"Error processing document {documentID}: {ex.StackTrace} - {ex.Message}");
        throw;
      }
    }

    // Get document metadata
    internal async Task<dynamic?> GetDocumentMetadataAsync(string guid, string apiKey)
    {
      string url = $"https://api.lucid.co/documents/{guid}/contents";
      var client = new RestClient(url);
      RestRequest request = CreateRestRequest(apiKey);

      var response = await client.GetAsync(request);
      if (response is null || !response.IsSuccessful || response.Content == null) return null;

      return Newtonsoft.Json.JsonConvert.DeserializeObject(response.Content);
    }

    // Download page as an image
    internal async Task<(bool success, string err)> DownloadImageToFileAsync(string apiKey, string url, string fileName, string contentType)
    {
      var client = new RestClient(url);
      RestRequest request = CreateRestRequest(apiKey);
      request.AddHeader("accept", $"{contentType}");

      try
      {
        var response = await client.DownloadDataAsync(request);

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
  }
}