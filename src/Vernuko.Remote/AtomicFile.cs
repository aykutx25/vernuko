namespace Vernuko.Remote;

/// <summary>Writes a file so that a power cut leaves either the old or the new content, never half of it.</summary>
internal static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] content)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }
}
