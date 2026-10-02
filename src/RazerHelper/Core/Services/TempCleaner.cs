namespace RazerHelper.Core.Services;

/// <summary>What a scan found, or a clean removed.</summary>
internal sealed record TempFilesResult(int Files, long Bytes, int Skipped = 0);

/// <summary>
/// Clears the user's own temporary folder: only files older than a day, so
/// nothing a program set down a moment ago goes, and only those Windows lets
/// go of, so a file a running program holds open is left where it is. Links
/// and junctions are never followed, so nothing outside the folder is ever
/// touched. The folder itself always stays.
/// </summary>
internal sealed class TempCleaner(string folder, TimeProvider? clock = null)
{
    /// <summary>Files newer than this are left alone.</summary>
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(1);

    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // Never into a link or junction, which may point anywhere.
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The user's temporary folder, %TEMP%.</summary>
    public static TempCleaner ForCurrentUser() => new(Path.GetTempPath());

    /// <summary>Counts what a clean would try to remove.</summary>
    public TempFilesResult Scan()
    {
        var files = 0;
        var bytes = 0L;

        foreach (var file in OldFiles())
        {
            files++;
            bytes += file.Length;
        }

        return new TempFilesResult(files, bytes);
    }

    /// <summary>Removes the old files it can, then the folders left empty.</summary>
    public TempFilesResult Clean()
    {
        var files = 0;
        var bytes = 0L;
        var skipped = 0;

        foreach (var file in OldFiles().ToList())
        {
            try
            {
                var length = file.Length;

                if (file.IsReadOnly)
                {
                    skipped++;
                    continue;
                }

                file.Delete();
                files++;
                bytes += length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Held open by a running program, or not the user's: it stays.
                skipped++;
            }
        }

        RemoveEmptyFolders();
        return new TempFilesResult(files, bytes, skipped);
    }

    private IEnumerable<FileInfo> OldFiles()
    {
        if (!Directory.Exists(folder))
            yield break;

        var cutoff = _clock.GetUtcNow().UtcDateTime - MinimumAge;

        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", Walk))
        {
            if (file.LastWriteTimeUtc < cutoff && file.CreationTimeUtc < cutoff)
                yield return file;
        }
    }

    // Deepest first, so a folder emptied by its children's removal goes too.
    // Only folders made over a day ago: a program may have just made one to use.
    private void RemoveEmptyFolders()
    {
        if (!Directory.Exists(folder))
            return;

        var cutoff = _clock.GetUtcNow().UtcDateTime - MinimumAge;

        var folders = new DirectoryInfo(folder)
            .EnumerateDirectories("*", Walk)
            .Where(each => each.CreationTimeUtc < cutoff)
            .OrderByDescending(each => each.FullName.Length)
            .ToList();

        foreach (var each in folders)
        {
            try
            {
                if (!each.EnumerateFileSystemInfos().Any())
                    each.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
