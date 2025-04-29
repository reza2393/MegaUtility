using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace MegaUtility.ArchiveFile;

public class ArchiveProcess : IDisposable
{
    private readonly IArchive _archive;
    private readonly string _archivePath;

    public ArchiveProcess(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found.", archivePath);
        _archivePath = archivePath;
        _archive = ArchiveFactory.Open(archivePath);
    }

    public List<IArchiveEntry> ListEntries()
        => _archive.Entries.ToList();

    public IEnumerable<IArchiveEntry> FilterEntries(Func<IArchiveEntry, bool> predicate)
        => _archive.Entries.Where(predicate);

    public IList<T> ProcessTextFilesInArchive<T>(Func<string, T> func, string splitRow = "\n", bool isParallelProcess = true)
    {
        var textFiles = FilterEntries(e => e.Key.EndsWith(".txt")).ToList();
        List<T> result = new List<T>();
        ProcessEntries(textFiles, entry =>
        {
            using var stream = entry.OpenEntryStream();
            using var reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            // پردازش محتوا
            var rows = content.Split(splitRow);
            if (isParallelProcess)
                Parallel.ForEach(rows, row => result.Add(func(row)));
            else
                foreach (var row in rows)
                    result.Add(func(row));
        });
        return result;
    }

    public void ProcessEntries(IEnumerable<IArchiveEntry> entries, Action<IArchiveEntry> action)
    {
        foreach (var entry in entries)
            action(entry);
    }

    public void ProcessEntriesParallel(IEnumerable<IArchiveEntry> entries, Action<IArchiveEntry> action)
    {
        Parallel.ForEach(entries, entry => action(entry));
    }

    // متد کمکی برای تعیین و ساخت مسیر مقصد
    private string GetOrCreateTargetDirectory(string? customPath)
    {
        string targetDir;
        if (string.IsNullOrWhiteSpace(customPath))
        {
            var archiveDir = Path.GetDirectoryName(_archivePath)!;
            var archiveName = Path.GetFileNameWithoutExtension(_archivePath);  // نام فایل بدون پسوند :contentReference[oaicite:0]{index=0}
            targetDir = Path.Combine(archiveDir, archiveName);
        }
        else
        {
            targetDir = customPath;
        }

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);  // ایجاد بازگشتی فولدر در صورت عدم وجود :contentReference[oaicite:1]{index=1}

        return targetDir;
    }

    public void ExtractEntry(IArchiveEntry entry, string? destinationPath = null, bool overwrite = true)
    {
        try
        {
            var targetDir = GetOrCreateTargetDirectory(destinationPath);
            entry.WriteToDirectory(
                targetDir,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = overwrite }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting {entry.Key}: {ex.Message}");
        }
    }

    public void ExtractAll(string? outputDirectory = null, bool overwrite = true)
    {
        try
        {
            var targetDir = GetOrCreateTargetDirectory(outputDirectory);
            _archive.WriteToDirectory(
                targetDir,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = overwrite }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting archive '{_archivePath}': {ex.Message}");
        }
    }

    public void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    public void Dispose()
        => (_archive as IDisposable)?.Dispose();
}
