# AgnosticUpdaterApp

A lightweight, agnostic .NET tool designed to fetch the latest release of any asset from a public GitHub repository, download it, extract it (or copy it directly if it's not a compressed archive), and optionally run a post-execution command-line script (like a `.bat`, `.cmd`, or executable) in a fire-and-forget manner.

---

## Features

- **Repository Agnostic:** Works with any public GitHub repository and release assets.
- **Smart Archive Handling:** Automatically detects compressed archive formats (such as `.zip`, `.7z`, `.rar`, `.tar`, `.gz`, `.tgz`, `.bz2`, `.xz`, `.cab`, `.iso`, `.wim`, etc.) and extracts them to the target folder using the native 7-Zip engine.
- **Direct File Downloads:** Non-compressed files (like `.exe`, `.bat`, `.sh`, or any standalone binary files) are downloaded and copied directly into the target folder without extraction.
- **Post-Execution Scripting:** Spawns an external task (e.g., an installation batch file) immediately after successfully updating.
- **Flexible Configuration:** Uses `appsettings.json` to configure URL templates and custom JSON parameters.

---

## Configuration (`appsettings.json`)

By default, the application uses the standard GitHub API structure. You can customize the behavior in `appsettings.json`:

```json
{
  "GitHubSettings": {
    "ReleasesApiUrlTemplate": "https://api.github.com/repos/{repo}/releases",
    "DownloadUrlTemplate": "https://github.com/{repo}/releases/download/{tag}/{file}",
    "TagNameJsonProperty": "tag_name"
  }
}
```

## Prerequisites

- Runtime: .NET 10.0 SDK or later.
- Operating System: Windows (due to SevenZipExtractor native DLL dependency bindings for x64 / x86 platforms).

## Usage

Run the compiled executable or use the dotnet run command followed by the required arguments.

### Syntax

```cmd
UpdaterApp <repo> <file> <target_folder> [optional_post_exec]
```

### Parameters

1. `<repo>` (Required): The GitHub repository in standard `owner/repository` format.

2. `<file>` (Required): The specific asset filename to look for in the latest release.

3. `<target_folder>` (Required): The local folder path where the downloaded release asset will be extracted or copied.

4. `[optional_post_exec]` (Optional): A path to a local executable, `.bat`, or `.cmd` file to trigger immediately after installation completes.

### Example

```cmd
UpdaterApp "oven-sh/bun" "bun-windows-x64.zip" "C:\tools\bun" "C:\tools\bun\install.bat"
```

## How to Build & Publish for Release

To distribute a optimized, self-contained, or framework-dependent folder containing all the required native DLL libraries (`7z.dll` wrappers in `x86/x64`), use `dotnet publish`.

### Create Framework-Dependent Release Bundle

This produces a light build folder ready to zip and distribute:

```cmd
dotnet publish -c Release
```

Your release files will be located in:

`./bin/release/net10.0/publish`

> Note: For extraction to succeed on your user's machine, the compiled `UpdaterApp.exe` must be bundled alongside `appsettings.json` and the `x86/` and `x64/` folders containing the native `7z.dll` libraries.
