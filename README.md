# Lucid Diagram Exporter

A command-line application built with .NET to export Lucidchart diagrams as image files. This tool supports exporting multiple documents in different formats and crop sizes, processing each page within a document, and saving the output locally.

## Features

- Export diagrams from Lucidchart via the Lucid API.
- Support for multiple documents via a comma-separated list of IDs.
- Saves exported images to a local folder with meaningful filenames.

## Prerequisites

1. **.NET SDK**: Install the .NET SDK (6.0 or later). [Download here](https://dotnet.microsoft.com/download).
2. **Lucid API Key**: Obtain your API key from [Lucid API](https://lucid.co/api).
3. **Environment Variable**: Set the API key in the environment variable `LUCID_API_KEY`.

### Setting the Environment Variable

#### Windows
```cmd
set LUCID_API_KEY=your-api-key
```

#### macOS/Linux
```bash
export LUCID_API_KEY=your-api-key
```

### Usage
#### Command Syntax
```bash
dotnet run "<documentIds>" [exportType] [cropSize]
```
- [documentIds]: Comma-separated Lucid document IDs (required).

### Examples
Export Multiple Documents as PNG with Crop
```bash
LucidExport.exe "doc1,doc2,doc3" PNG "300x200"
```

### Output
Exported images are saved in the ExportedPages folder, named using the following pattern:

### License
This project is licensed under the MIT License. See the LICENSE file for details.