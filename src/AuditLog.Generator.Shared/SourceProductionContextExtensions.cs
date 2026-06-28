#nullable enable
using Microsoft.CodeAnalysis;
using System.Text;

namespace AuditLog.Generator;

public static class SourceProductionContextExtensions
{
public static void AddHintSource(SourceProductionContext context, string ns, string fileName, string source)
{
    var hintName = string.IsNullOrEmpty(ns) || ns.StartsWith("<")
        ? fileName
        : $"{ns}.{fileName}";

    context.AddSource(hintName, source);
}
}
