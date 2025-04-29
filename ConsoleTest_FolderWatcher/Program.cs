using System;
using System.IO;
using MegaUtility.IOWorker;

namespace FolderWatcherConsoleTest
{
    class Program
    {
        private static string _testDirectory = Path.Combine(Path.GetTempPath(), "FolderWatcherTest");

        static void Main()
        {
            // مراحل تست:
            SetupTestDirectory();
            var watcher = CreateAndConfigureWatcher();

            try
            {
                // ایجاد فایل تست
                CreateTestFiles();

                Console.WriteLine("next ...");
                System.Threading.Thread.Sleep(40);
                Console.WriteLine("next ...");



                // شبیه‌سازی تغییرات فایل
                ModifyTestFiles();

                // منتظر دریافت رویدادها بمانید
                Console.WriteLine("Wait for process event ...");
                System.Threading.Thread.Sleep(5000);
            }
            finally
            {
                watcher.Dispose();
                CleanupTestDirectory();
            }
        }

        static GenericFolderWatcher CreateAndConfigureWatcher()
        {
            var watcher = new GenericFolderWatcher();

            // تنظیمات هندلرها
            watcher.AddHandler("captcha.txt", path =>
                Console.WriteLine($"Captcha file process : {path}")
            );

            watcher.AddHandler(".txt", path =>
                Console.WriteLine($"text file process : {path}")
            );

            watcher.AddAllowedExtension(".txt");

            // رویداد خطا
            watcher.ErrorOccurred += error =>
                Console.WriteLine($"Error : {error}");

            // شروع نظارت
            watcher.StartMonitoring(_testDirectory);
            return watcher;
        }

        static void SetupTestDirectory()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);

            Directory.CreateDirectory(_testDirectory);
            Console.WriteLine($"folder test created : {_testDirectory}");
        }

        static void CreateTestFiles()
        {
            // ایجاد فایل مجاز
            File.WriteAllText(Path.Combine(_testDirectory, "test1.txt"), "محتوای اولیه");

            // ایجاد فایل غیرمجاز
            File.WriteAllText(Path.Combine(_testDirectory, "ignore.pdf"), "این نادیده گرفته می‌شود");
        }

        static void ModifyTestFiles()
        {
            // تغییر فایل موجود
            File.AppendAllText(Path.Combine(_testDirectory, "test1.txt"), " - محتوای اضافه شده");

            // ایجاد فایل جدید
            File.WriteAllText(Path.Combine(_testDirectory, "captcha.txt"), "ABC123");

            // ایجاد فایل قفل‌شده
            var lockedFilePath = Path.Combine(_testDirectory, "locked.txt");
            File.WriteAllText(lockedFilePath, "محتوای قفل‌شده");
            //using (var stream = File.Open(lockedFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            //{
            //    // تلاش برای دسترسی همزمان
            //    // شما با استفاده از File.Open و پارامتر FileShare.None، فایل را به صورت انحصاری باز کرده‌اید
            //    // این پارامتر مانع دسترسی همزمان هرگونه عملیات دیگری(حتی از همان پروسه) به فایل می‌شود
            //    // وقتی File.AppendAllText تلاش می‌کند فایل را باز کند، با خطای IOException مواجه می‌شود چون فایل قفل است
            //    try { File.AppendAllText(lockedFilePath, "این باعث خطا می‌شود"); }
            //    catch { /* انتظار خطا */ }
            //}
        }

        static void CleanupTestDirectory()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
            Console.WriteLine("clear folder .");
        }
    }
}