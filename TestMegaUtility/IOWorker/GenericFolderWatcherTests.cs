using MegaUtility.IOWorker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestMegaUtility.IOWorker;

public class GenericFolderWatcherTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly GenericFolderWatcher _watcher;
    private bool _handlerInvoked;
    private string? _processedFilePath;

    public GenericFolderWatcherTests()
    {
        // مقداردهی اولیه پوشه تست
        _testDirectory = Path.Combine(Path.GetTempPath(), "TestFolder");
        Directory.CreateDirectory(_testDirectory);

        // مقداردهی کلاس نظارت
        _watcher = new GenericFolderWatcher();
        _watcher.AddAllowedExtension(".txt");
        _watcher.AddHandler("test.txt", path =>
        {
            _handlerInvoked = true;
            _processedFilePath = path;
        });
        _watcher.StartMonitoring(_testDirectory);
    }

    [Fact]
    public void Test_FileCreated_HandlerInvoked()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "Sample Content");

        // Assert (با تاخیر برای پردازش رویداد)
        Assert.True(SpinWait.SpinUntil(() => _handlerInvoked, TimeSpan.FromSeconds(5)));
        Assert.Equal(testFilePath, _processedFilePath);
    }

    [Fact]
    public void Test_DisallowedExtension_Ignored()
    {
        // Arrange
        var invalidFilePath = Path.Combine(_testDirectory, "invalid.jpg");
        File.WriteAllText(invalidFilePath, "Sample Content");

        // Assert
        Assert.False(_handlerInvoked); // هندلر فراخوانی نشده است
    }

    public void Dispose()
    {
        _watcher.StopMonitoring();
        Directory.Delete(_testDirectory, true);
    }

    /// <summary>
    /// تست تغییر فایل و جلوگیری از پردازش مکرر:
    /// </summary>
    [Fact]
    public void Test_FileChanged_Throttling()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "First Write");
        Assert.True(SpinWait.SpinUntil(() => _handlerInvoked, TimeSpan.FromSeconds(5)));

        _handlerInvoked = false;
        File.WriteAllText(testFilePath, "Second Write");

        // Assert
        Assert.False(SpinWait.SpinUntil(() => _handlerInvoked, TimeSpan.FromMilliseconds(200))); // تاخیر 300 میلی‌ثانیه
    }

    /// <summary>
    /// تست خطا در دسترسی به فایل:
    /// </summary>
    [Fact]
    public async Task Test_FileAccess_ErrorOccurred()
    {
        // Arrange
        var errorMessages = new List<string>();
        var tcs = new TaskCompletionSource<bool>();
        var lockedFilePath = Path.Combine(_testDirectory, "locked.txt");

        // مقداردهی اولیه فایل
        File.WriteAllText(lockedFilePath, "Locked Content");

        // Subscribe to error event
        _watcher.ErrorOccurred += (message) =>
        {
            errorMessages.Add(message);
            tcs.TrySetResult(true); // Notify test when error occurs
        };

        // Act
        using (var stream = File.Open(lockedFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // تلاش برای دسترسی همزمان
            var task = Task.Run(() => File.WriteAllText(lockedFilePath, "New Content"));

            // Wait for error or timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));

            // Assert
            //Assert.Contains(completedTask, t => t == tcs.Task); // Ensure error occurred
            Assert.Contains(errorMessages, msg => msg.Contains("locked.txt")); // Check specific error
        }
    }
}
