using ZArchive;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ListArchive <archive.zar>");
    return 1;
}

using ZArchiveReader archive = ZArchiveReader.Open(args[0]);
foreach (ZArchiveEntry entry in archive.EnumerateEntries(recursive: true))
{
    Console.WriteLine(entry.IsFile
        ? $"File: {entry.FullName} ({entry.Length} bytes)"
        : $"Directory: {entry.FullName}");
}
return 0;
