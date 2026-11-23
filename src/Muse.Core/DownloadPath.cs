using System.Runtime.InteropServices;

namespace Muse.Core;

/// <summary>
/// Download destination handling: never overwrite an existing file, and reserve
/// the destination before any bytes are written. Port of uniqueDownloadPath()
/// in electron/utils.cjs.
/// </summary>
public static class DownloadPath
{
    /// <summary>Used when the download has no usable name.</summary>
    public const string FallbackFileName = "muse-download";

    /// <summary>
    /// Reduces a server-provided name to something the Windows file system
    /// accepts and that cannot escape the downloads directory.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);

        if (string.IsNullOrWhiteSpace(name))
        {
            return FallbackFileName;
        }

        name = name.Trim().TrimEnd('.', ' ');

        foreach (var character in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(character, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? FallbackFileName : name;
    }

    /// <summary>
    /// Atomically reserves a fresh file in <paramref name="downloadsDir"/> and
    /// returns its full path. Existing files are never reused: the second
    /// "report.pdf" becomes "report (1).pdf", then "report (2).pdf".
    /// Throws when the destination cannot be reserved, so callers can cancel
    /// the download instead of writing over the user's data.
    /// </summary>
    public static string Reserve(string downloadsDir, string? fileName)
    {
        var safeName = SanitizeFileName(fileName);
        var stem = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);

        var candidate = Path.Combine(downloadsDir, safeName);
        for (var suffix = 1; ; suffix += 1)
        {
            try
            {
                // CreateNew is the atomic reservation: it fails if the file
                // already exists, including against other processes.
                using var stream = new FileStream(
                    candidate,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate))
            {
                candidate = Path.Combine(downloadsDir, $"{stem} ({suffix}){extension}");
            }
        }
    }

    /// <summary>The user's Downloads folder, the same target Electron used.</summary>
    public static string DefaultDownloadsDirectory()
    {
        return DownloadsFolder.GetPath()
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
    }
}

internal static class DownloadsFolder
{
    // FOLDERID_Downloads: {374DE290-123F-4565-9164-39C4925E467B}
    private static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string? GetPath()
    {
        try
        {
            var result = SHGetKnownFolderPath(Downloads, 0, IntPtr.Zero, out var path);
            if (result != 0)
            {
                return null;
            }

            var value = Marshal.PtrToStringUni(path);
            Marshal.FreeCoTaskMem(path);
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetKnownFolderPath(
        Guid folderId,
        int flags,
        IntPtr token,
        out IntPtr path);
}
