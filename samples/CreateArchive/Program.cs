using ZArchive;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: CreateArchive <source-directory> <archive.zar>");
    return 1;
}

ZArchiveFile.CreateFromDirectory(args[0], args[1]);
Console.WriteLine($"Created '{args[1]}' from '{args[0]}'.");
return 0;
