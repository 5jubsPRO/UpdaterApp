using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SevenZipExtractor; // Changed from to SevenZipExtractor

// 1. Validate Command-Line Arguments
if (args.Length < 2)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: Missing parameters.");
    Console.ResetColor();
    Console.WriteLine("\nUsage: UpdaterApp <repo> <file> [optional_post_exec]");
    Console.WriteLine("Example: UpdaterApp \"oven-sh/bun\" \"bun-windows-x64.zip\" \"C:\\MyFolder\\install.bat\"");
    return;
}

// Read inputs and trim any trailing whitespaces
string repo = args[0].Trim();
string file = args[1].Trim();
string? postExecutionFile = args.Length >= 3 ? args[2].Trim() : null;

// 2. Load appsettings.json Configuration
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
IConfiguration config = builder.Build();

string releasesTemplate = config["GitHubSettings:ReleasesApiUrlTemplate"] ?? "https://api.github.com/repos/{repo}/releases";
string downloadTemplate = config["GitHubSettings:DownloadUrlTemplate"] ?? "https://github.com/{repo}/releases/download/{tag}/{file}";
string jsonProperty = config["GitHubSettings:TagNameJsonProperty"] ?? "tag_name";

// Build the release URL dynamically
string releasesUrl = releasesTemplate.Replace("{repo}", repo);

// 3. Setup HTTP Client (GitHub requires a User-Agent header)
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AgnosticUpdaterApp", "1.0"));

// 4. Fetch the latest release tag name
Console.WriteLine("Determining latest release...");
string tag = string.Empty;

try
{
    string jsonResponse = await httpClient.GetStringAsync(releasesUrl);
    using var jsonDocument = JsonDocument.Parse(jsonResponse);
    
    if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array && jsonDocument.RootElement.GetArrayLength() > 0)
    {
        var latestRelease = jsonDocument.RootElement[0];
        if (latestRelease.TryGetProperty(jsonProperty, out var tagProperty))
        {
            tag = tagProperty.GetString() ?? string.Empty;
        }
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error fetching or parsing releases: {ex.Message}");
    Console.ResetColor();
    return;
}

if (string.IsNullOrEmpty(tag))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Could not retrieve '{jsonProperty}' property from the target API.");
    Console.ResetColor();
    return;
}

Console.WriteLine($"Latest Tag Identified: {tag}");

// 5. Setup file structures
string downloadUrl = downloadTemplate
    .Replace("{repo}", repo)
    .Replace("{tag}", tag)
    .Replace("{file}", file);

// Dynamically extract the name and extension (handles .zip, .7z, and even .tar.gz)
int firstDotIndex = file.IndexOf('.');
string name = firstDotIndex > 0 ? file.Substring(0, firstDotIndex) : file;
string extension = firstDotIndex > 0 ? file.Substring(firstDotIndex) : string.Empty;

string downloadedFile = $"{name}-{tag}{extension}";
string tempDir = $"{name}-{tag}";

// 6. Download the release archive
Console.WriteLine($"Downloading latest release from: {downloadUrl}");
try
{
    byte[] fileBytes = await httpClient.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(downloadedFile, fileBytes);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Download failed: {ex.Message}");
    Console.ResetColor();
    return;
}

// 7. Extract the archive using SevenZipExtractor
Console.WriteLine("Extracting release files...");
try
{
     //SevenZipExtractor loads its native dll and handles extraction
     using (var archiveFile = new ArchiveFile(downloadedFile))
     {
        archiveFile.Extract(tempDir);
     }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Extraction failed: {ex.Message}");
    Console.ResetColor();
    return;
}

// 8. Clean up destination and move directory structures
Console.WriteLine("Cleaning up target folder...");
try
{
    if (Directory.Exists(name))
    {
        Directory.Delete(name, true);
    }
}
catch (IOException ex)
{
    Console.WriteLine($"Warning: Could not remove old folder '{name}': {ex.Message}");
}

Console.WriteLine("Moving files to final destination...");
try
{
    string extractedSubfolder = Path.Combine(tempDir, name);
    if (Directory.Exists(extractedSubfolder))
    {
        Directory.Move(extractedSubfolder, name);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Warning: Extracted subfolder '{extractedSubfolder}' not found. Cannot perform final move.");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Failed to move final directories: {ex.Message}");
    Console.ResetColor();
    return;
}

// 9. Remove temporary files
Console.WriteLine("Removing temp files...");
try
{
    if (File.Exists(downloadedFile))
    {
        File.Delete(downloadedFile);
    }

    if (Directory.Exists(tempDir))
    {
        Directory.Delete(tempDir, true);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Failed to perform clean-up of temporary items: {ex.Message}");
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n[SUCCESS] Main routines completed successfully!");
Console.ResetColor();

// 10. Fire-and-Forget Post-Execution Executable
if (!string.IsNullOrEmpty(postExecutionFile))
{
    Console.WriteLine($"Attempting to trigger post-execution script: '{postExecutionFile}'...");
    
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = postExecutionFile,
            UseShellExecute = true, // Enables OS shell resolution (crucial for .bat / .cmd)
            CreateNoWindow = false
        };

        // Fire-and-forget: Spawns the process and does not wait
        Process.Start(startInfo);
        Console.WriteLine("Post-execution script triggered. Spawning separate process...");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[WARNING] Could not execute the post-execution file.");
        Console.WriteLine($"Reason: {ex.Message}");
        Console.WriteLine("Note: Arguments are not supported in the third parameter.");
        Console.ResetColor();
    }
}
