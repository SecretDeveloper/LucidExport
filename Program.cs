using System.CommandLine;
using RestSharp;
using System.Threading.Tasks;
using System.Diagnostics;

namespace LucidExporter
{
  class Program
  {
    static async Task Main(string[] args)
    {
      try
      {
        var documentsFileOption = new Option<FileInfo?>(
                        name: "-f",
                        description: "A file containing a Lucidchart document id on each line.");
        var documentIdOption = new Option<string?>(
                        name: "-d",
                        description: "The document id (guid from url) of the Lucidchart document you want to export.");
        var outputFolderOption = new Option<DirectoryInfo?>(
                        name: "-o",
                        description: "The location to export images to.",
                        getDefaultValue: () => new DirectoryInfo("./LucidExports"));
        var verboseOption = new Option<bool>(
                        name: "-v",
                        description: "Enable verbose output.",
                        getDefaultValue: () => false);
        var lucidAPIOption = new Option<string>(
                        name: "-a",
                        description: "Lucidchart API Key.  Will try to read $LUCID_API_KEY environment variable if not provided.",
                        getDefaultValue: () => "");

        var rootCommand = new RootCommand("LucidChart Image Exporter.  This application can extract documents from Lucidchart to images.  Each page in the document is exported as a single PNG, unfortunately the Lucidchart API does not support SVG exports.");
        rootCommand.AddOption(documentsFileOption);
        rootCommand.AddOption(documentIdOption);
        rootCommand.AddOption(outputFolderOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(lucidAPIOption);

        var exporter = new LucidExporter();

        rootCommand.SetHandler(async (documentsFile, documentId, outputFolder, verbose, apiKey) =>
        {
          // Show help when no arguments are provided
          if (args.Length == 0)
          {
            Console.WriteLine("No arguments provided. Use '--help' for usage instructions.");
            await rootCommand.InvokeAsync(["--help"]);
            return; // Exit after showing help
          }

          exporter.Verbose = verbose;
          if (string.IsNullOrEmpty(apiKey)) apiKey = exporter.GetAPIKey();
          if (string.IsNullOrEmpty(apiKey))
          {
            exporter.LogError("Please provide an API key for Lucidchart via an argument or environment variable.");
            return;
          }

          if (documentsFile != null)
          {
            await exporter.ProcessFileAsync(apiKey, documentsFile, outputFolder!);
          }
          else if (!string.IsNullOrWhiteSpace(documentId))
          {
            await exporter.ProcessDocumentAsync(apiKey, documentId!, outputFolder!);
          }
        }, documentsFileOption, documentIdOption, outputFolderOption, verboseOption, lucidAPIOption);

        var sw = new Stopwatch();
        sw.Start();
        await rootCommand.InvokeAsync(args);
        sw.Stop();
        exporter.Log($"Took {sw.ElapsedMilliseconds}ms");

      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"An error occurred: {ex.Message}");
      }
    }
  }
}