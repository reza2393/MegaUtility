using System.Diagnostics;

namespace MegaUtility.IOWorker;

public class GenericFolderWatcher() : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, Action<string>> _fileHandlers = new();
    private readonly List<string> _allowedExtensions = new();
    private readonly Dictionary<string, DateTime> _fileLastWriteTimes = new(); // ذخیره زمان آخرین پردازش هر فایل
    private readonly object _lock = new();

    public event Action<string>? FileProcessed;
    public event Action<string>? ErrorOccurred;

    public void AddHandler(string filePattern, Action<string> handler)
    {
        _fileHandlers[filePattern.ToLower()] = handler;
    }

    public void AddAllowedExtension(string extension)
    {
        _allowedExtensions.Add(extension.ToLower());
    }

    public void StartMonitoring(string folderPath, bool includeSubdirectories = false)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Path '{folderPath}' not found.");

        StopMonitoring();

        _watcher = new FileSystemWatcher
        {
            Path = folderPath,
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                var filePath = e.FullPath;
                var fileName = Path.GetFileName(filePath);

                // ۱. بررسی وجود فایل و قفل نبودن آن
                if (!IsFileReady(filePath))
                    return;

                // ۲. بررسی تاخیر برای هر فایل جداگانه
                lock (_lock)
                {
                    if (_fileLastWriteTimes.TryGetValue(filePath, out var lastWriteTime))
                    {
                        if (DateTime.UtcNow - lastWriteTime < TimeSpan.FromMilliseconds(200))
                            return;
                    }

                    _fileLastWriteTimes[filePath] = DateTime.UtcNow;
                }

                // ۳. پردازش فایل
                await Task.Run(() => ProcessFile(filePath));
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error: {ex.Message}");
            }
        });
    }

    private bool IsFileReady(string filePath)
    {
        try
        {
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                return fs.Length > 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsFileReadyAsync(string filePath)
    {
        var timeout = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using (var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fs.Length > 0)
                        return true;
                }
            }
            catch { }

            await Task.Delay(20); // منتظر ماندن غیرمسدودکننده
        }

        return false;
    }

    private void ProcessFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var extension = Path.GetExtension(filePath).ToLower();
        if (_allowedExtensions.Count > 0 && !_allowedExtensions.Contains(extension))
            return;

        foreach (var handler in _fileHandlers)
        {
            if (MatchesPattern(filePath, handler.Key))
            {
                handler.Value(filePath);
                FileProcessed?.Invoke(filePath);
                return;
            }
        }

        // Handle unmatched files
        FileProcessed?.Invoke(filePath);
    }

    private bool MatchesPattern(string filePath, string pattern)
    {
        // Custom matching logic (extension, filename, etc.)
        return filePath.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileName(filePath).Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public void StopMonitoring()
    {
        _watcher?.Dispose();
        _watcher = null!;
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}

