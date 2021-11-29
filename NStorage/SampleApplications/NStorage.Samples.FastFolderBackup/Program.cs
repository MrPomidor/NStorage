using Mono.Options;
using NStorage;
using System.Collections.Concurrent;
using static ApplicationHelper;
using static ConsoleHelper;

// plan
// 1. read folder to backup
// 2. read target storage folder
// 3. init storage
// 4. get all files -> compress all files
// 5. flush and dispose (flush mode deferred with small interval)
// 6. out usage statistics (files count, common size, size of result storage)
// 7. Exit 0 if no errors, 1 if errors


Write("Fast folder backup app. 1.0");
Write("Handling console arguments ...");

int? exitCode = HandleConsoleArguments(args, out var sourceFolder, out var storageFolder);
if (exitCode.HasValue)
    return exitCode.Value;

Br();
Write($"Source folder: \"{sourceFolder}\"");
Write("Handling files ...");

var fileInfos = new ConcurrentDictionary<string, long>();
var errors = new ConcurrentDictionary<string, Exception>();
exitCode = HandleFiles(sourceFolder: sourceFolder, storageFolder: storageFolder, fileInfos, errors, out var totalFiles);
if (exitCode.HasValue)
    return exitCode.Value;

ShowErrors(errors);
ShowSummary(totalFiles, fileInfos, errors, storageFolder);

return errors.IsEmpty ? 0 : 1;


public static class ApplicationHelper
{
    // returns exit code if cannot handle, or null if can continue
    public static int? HandleConsoleArguments(string[] args, out string sourceFolderResult, out string storageFolderResult)
    {
        sourceFolderResult = "";
        storageFolderResult = "";

        var sourceFolder = "";
        var storageFolder = "";
        var showHelp = false;
        var options = new OptionSet
        {
            { "s|source-folder=", "(Mandatory) Folder to fetch data from", (val) => sourceFolder = val },
            { "t|target-folder=", "(Mandatory) Folder to create storage", (val) => storageFolder = val },
            { "h|help", "Show help", (_) => showHelp = true }
        };

        try
        {
            var parsed = options.Parse(args);
            if (showHelp)
            {
                ShowHelp(options);
                return 0;
            }

            if (string.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                WriteError("Source folder invalid");
                throw new Exception();
            }
            if (string.IsNullOrEmpty(storageFolder))
            {
                WriteError("Storage folder invalid");
                throw new Exception();
            }
            if (!Directory.Exists(storageFolder))
            {
                try
                {
                    Directory.CreateDirectory(storageFolder);
                }
                catch
                {
                    WriteError("Could not create storage folder");
                    throw;
                }
            }

            sourceFolderResult = sourceFolder;
            storageFolderResult = storageFolder;

            return null;
        }
        catch
        {
            WriteError("Invalid options");
            WriteError("Use \"--help\" to get usage instructions");
            return -1;
        }
    }

    private static void ShowHelp(OptionSet p)
    {
        Write("Sample application, which create compressed backup of passed folder using NStorage.");
        Write();
        Write("Options:");
        p.WriteOptionDescriptions(Console.Out);
    }

    // returns exit code if cannot handle, or null if can continue
    public static int? HandleFiles(string sourceFolder, string storageFolder, ConcurrentDictionary<string, long> fileInfos, ConcurrentDictionary<string, Exception> errors, out int totalFiles)
    {
        totalFiles = 0;

        BinaryStorage storage;
        try
        {
            storage = new BinaryStorage(new StorageConfiguration(storageFolder).SetFlushModeDeferred(50));
        }
        catch (Exception ex)
        {
            WriteError($"Cannot create storage: {ex.Message}");
            return -1;
        }

        try
        {
            var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            totalFiles = files.Length;

            files.AsParallel()
                .ForAll((fileName) =>
                {
                    try
                    {
                        using var fileStream = File.OpenRead(fileName);

                        storage.Add(fileName, fileStream, StreamInfo.Compressed);

                        _ = fileInfos.TryAdd(fileName, fileStream.Length);
                    }
                    catch (Exception ex)
                    {
                        _ = errors.TryAdd(fileName, ex);
                    }
                });
        }
        catch (Exception ex)
        {
            WriteError($"Error occurred while reading files: {ex.Message}");
            return -1;
        }
        finally
        {
            try
            {
                storage.Dispose();
            }
            catch { }
        }

        return null;
    }

    public static void ShowErrors(IDictionary<string, Exception> errors)
    {
        if (errors.Count > 0)
        {
            Br();
            WriteError("ERRORS during files saving");
            int i = 1;
            foreach (var error in errors)
            {
                WriteError($"{i}::File: {error.Key}");
                WriteError($"{i}::Error: {error.Value.GetType().Name} - {error.Value.Message}");
                i++;
            }
        }
    }

    public static void ShowSummary(int totalFiles, IDictionary<string, long> fileInfos, IDictionary<string, Exception> errors, string storageFolder)
    {
        Br();
        Write("SUMMARY");
        Write($"Files in source folder: {totalFiles}");
        Write($"Files handled:          {fileInfos.Count}");
        Write($"Files not handled:      {errors.Count}");
        Write($"Total handled size:     {FormatBytes(fileInfos.Values.Sum())}");
        Write($"Storage location:       \"{storageFolder}\"");

        var indexSizeBytes = new FileInfo(Path.Combine(storageFolder, BinaryStorage.IndexFile)).Length;
        var storageSizeBytes = new FileInfo(Path.Combine(storageFolder, BinaryStorage.StorageFile)).Length;
        var storageSize = indexSizeBytes + storageSizeBytes;
        Write($"Storage size:           {FormatBytes(storageSize)}");
        Br();
    }
}

public static class ConsoleHelper
{
    public static void Write(string? text = null)
    {
        Console.WriteLine(text);
    }

    public static void WriteError(string text)
    {
        var fgColorBackup = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ForegroundColor = fgColorBackup;
    }

    private static readonly string _br = new('-', 40);
    public static void Br()
    {
        Console.WriteLine(_br);
    }

    public static string FormatBytes(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        if (byteCount == 0)
            return "0" + suf[0];
        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString() + suf[place];
    }
}