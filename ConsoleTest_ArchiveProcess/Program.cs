using MegaUtility.ArchiveFile;

namespace ConsoleTest_ArchiveProcess;

public class Program
{
    public static void Main()
    {
        ArchiveProcess archive = new ArchiveProcess("C:\\Users\\Reza\\Desktop\\test\\files.rar");
        var alls = archive.ListEntries();
       foreach (var entry in alls)
        {
            Console.WriteLine(entry.Key);
        }

        //archive.ExtractEntry(alls[1]);
        //archive.ExtractAll(null,true);

        var ss = archive.FilterEntries(x => x.Key.EndsWith(".txt"));

        //archive.CleanupDirectory("C:\\Users\\Reza\\Desktop\\test\\files");

        var strs = archive.ProcessTextFilesInArchive<string>(x => x.Split(",")[0], ";");
    }
}