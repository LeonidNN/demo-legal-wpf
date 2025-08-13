using System;
using System.IO;

namespace DemoLegal.Infrastructure.Files;

public static class PathService
{
    public static string GetCasesRoot()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var root = Path.Combine(docs, "DemoLegal", "Cases");
        Directory.CreateDirectory(root);
        return root;
    }

    public static string GetCaseFolder(Guid caseId)
    {
        var root = GetCasesRoot();
        var path = Path.Combine(root, caseId.ToString("D"));
        Directory.CreateDirectory(path);
        return path;
    }
}
