using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SevenZipExtractor;

#if DEBUG
args = new string[] {
    "<usuario>/<repo>",
    "arquivo.ext",
    "<Drive>:\\<Caminho>\\<Pasta>",
    "<Drive>:\\<Caminho>\\<ScriptDePósExecução>.bat"
};
#endif

// 1. Validate Command-Line Arguments (Minimum 3 parameters required now)
if (args.Length < 3)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: Missing parameters.");
    Console.ResetColor();
    Console.WriteLine("\nUsage: UpdaterApp <repo> <file> <target_folder> [optional_post_exec]");
    Console.WriteLine("Example: UpdaterApp \"oven-sh/bun\" \"bun-windows-x64.zip\" \"C:\\tools\\bun\" \"C:\\tools\\bun\\install.bat\"");
    return;
}

// Read inputs and trim any trailing whitespaces
string repo = args[0].Trim();
string file = args[1].Trim();
string targetFolder = args[2].Trim();
string? postExecutionFile = args.Length >= 4 ? args[3].Trim() : null;

// 2. Target Folder Creation & Verification
try
{
    if (!Directory.Exists(targetFolder))
    {
        Console.WriteLine($"Target folder '{targetFolder}' does not exist. Creating directory...");
        Directory.CreateDirectory(targetFolder);
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Failed to verify or create target folder: {ex.Message}");
    Console.ResetColor();
    return;
}

// 3. Load appsettings.json Configuration
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
IConfiguration config = builder.Build();

string releasesTemplate = config["GitHubSettings:ReleasesApiUrlTemplate"] ?? "https://api.github.com/repos/{repo}/releases/latest";
string downloadTemplate = config["GitHubSettings:DownloadUrlTemplate"] ?? "https://github.com/{repo}/releases/download/{tag}/{file}";
string jsonProperty = config["GitHubSettings:TagNameJsonProperty"] ?? "tag_name";

// Build the release URL dynamically
string releasesUrl = releasesTemplate.Replace("{repo}", repo);

// 4. Setup HTTP Client (GitHub requires a User-Agent header)
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AgnosticDownloaderApp", "1.0"));

// 5. Fetch the latest release tag name
Console.WriteLine("Determining latest release...");
string tag = string.Empty;

try
{
    string jsonResponse = await httpClient.GetStringAsync(releasesUrl);
    using var jsonDocument = JsonDocument.Parse(jsonResponse);

    //if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array &&
    if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
    {
        var latestRelease = jsonDocument.RootElement;//[0]
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

// 6. Setup dynamic URL structures & identify file type
string downloadUrl = downloadTemplate
    .Replace("{repo}", repo)
    .Replace("{tag}", tag)
    .Replace("{file}", file);

// Define common compressed archive extensions
string[] archiveExtensions = { 
    ".zip", ".7z", ".rar", ".tar", ".gz", ".gzip", ".tgz", 
    ".bz2", ".tbz2", ".xz", ".txz", ".cab", ".iso", ".wim", 
    ".lzma", ".z" 
};

string lowerFile = file.ToLowerInvariant();
bool isCompressed = archiveExtensions.Any(ext => lowerFile.EndsWith(ext));

string extension = Path.GetExtension(file);
string baseName = Path.GetFileNameWithoutExtension(file);

// Store downloaded file inside the system temp directory so we don't clutter the execution path
string tempDownloadedFile = Path.Combine(Path.GetTempPath(), $"{baseName}-{tag}{extension}");

// 7. Download the release asset
Console.WriteLine($"Downloading latest release from: {downloadUrl}");
try
{
    byte[] fileBytes = await httpClient.GetByteArrayAsync(downloadUrl);
    await File.WriteAllBytesAsync(tempDownloadedFile, fileBytes);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Download failed: {ex.Message}");
    Console.ResetColor();
    return;
}

// 8. Extract archive or copy file directly to target folder
if (isCompressed)
{
    Console.WriteLine($"Extracting release files directly to: '{targetFolder}'...");
    try
    {
        using (var archiveFile = new ArchiveFile(tempDownloadedFile))
        {
            archiveFile.Extract(targetFolder);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Extraction failed: {ex.Message}");
        Console.ResetColor();
        return;
    }
}
else
{
    string destinationFile = Path.Combine(targetFolder, file);
    Console.WriteLine($"Non-archive file detected. Copying file directly to: '{destinationFile}'...");
    try
    {
        File.Copy(tempDownloadedFile, destinationFile, overwrite: true);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to copy file to target folder: {ex.Message}");
        Console.ResetColor();
        return;
    }
}

// 9. Remove downloaded temporary file
Console.WriteLine("Removing downloaded temporary file...");
try
{
    if (File.Exists(tempDownloadedFile))
    {
        File.Delete(tempDownloadedFile);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Failed to clean up temporary file: {ex.Message}");
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
            UseShellExecute = true, // Enables OS shell resolution (crucial for .bat / .cmd files)
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
        Console.WriteLine("Note: Arguments are not supported in the fourth parameter.");
        Console.ResetColor();
    }
}
