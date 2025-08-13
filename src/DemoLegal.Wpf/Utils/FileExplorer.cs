using System.Diagnostics;

namespace DemoLegal.Wpf.Utils;

public static class FileExplorer
{
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
