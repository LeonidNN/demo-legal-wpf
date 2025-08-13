using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DemoLegal.Infrastructure.Documents;

/// <summary>
/// Простейший движок подстановки {{placeholders}} для текстовых шаблонов.
/// (Позже можно заменить на DOCX-шаблонизатор.)
/// </summary>
public static class TemplateEngine
{
    public static string Render(string templateText, IDictionary<string, string> vars)
    {
        var sb = new StringBuilder(templateText);
        foreach (var kv in vars)
            sb.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty);
        return sb.ToString();
    }

    public static string ReadTemplate(string fullPath)
        => File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : string.Empty;
}
