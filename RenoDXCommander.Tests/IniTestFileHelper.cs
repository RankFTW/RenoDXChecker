using System.Threading;

namespace RenoDXCommander.Tests;

/// <summary>
/// Helper for INI deploy tests that need to read/write shared AppData source files.
/// Uses a named mutex and retry logic to handle file contention from parallel tests
/// or external processes (e.g., the running RHI application).
/// </summary>
internal static class IniTestFileHelper
{
    private static readonly Mutex SharedMutex = new(false, "Global\\RdxcIniTestMutex");

    /// <summary>
    /// Writes bytes to a file with retry logic to handle transient file locks.
    /// </summary>
    public static void WriteWithRetry(string path, byte[] content, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                fs.Write(content, 0, content.Length);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(50 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Deletes a file with retry logic to handle transient file locks.
    /// </summary>
    public static void DeleteWithRetry(string path, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(50 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Reads all bytes from a file with retry logic.
    /// </summary>
    public static byte[]? ReadWithRetry(string path, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(50 * (i + 1));
            }
        }
        return null;
    }

    /// <summary>Acquires the shared mutex for exclusive access to source INI files.</summary>
    public static void AcquireLock() => SharedMutex.WaitOne(TimeSpan.FromSeconds(30));

    /// <summary>Releases the shared mutex.</summary>
    public static void ReleaseLock() => SharedMutex.ReleaseMutex();
}
