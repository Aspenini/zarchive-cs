using ZArchive;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ExtractArchive <archive.zar> <destination-directory>");
    return 1;
}

ZArchiveFile.ExtractToDirectory(args[0], args[1], overwrite: true);
Console.WriteLine($"Extracted '{args[0]}' to '{args[1]}'.");
return 0;
