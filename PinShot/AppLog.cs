using System;
using System.IO;

namespace PinShot
{
    internal static class AppLog
    {
        public static string Path => System.IO.Path.Combine(NoteStore.RootDir, "log.txt");

        public static void Write(string message)
        {
            try
            {
                string path = Path;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
