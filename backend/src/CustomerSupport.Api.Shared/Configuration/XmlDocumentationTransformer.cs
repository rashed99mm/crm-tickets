using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CustomerSupport.Api.Shared.Configuration;

/// <summary>
/// Copies the XML documentation comments on controllers and actions into the
/// published OpenAPI document.
/// </summary>
/// <remarks>
/// Every project sets GenerateDocumentationFile, which produces the XML but does
/// not by itself put a single word into the served document - so the prose that
/// was written for consumers never reached them. This transformer closes that
/// gap: summaries become operation summaries, remarks become descriptions, and
/// param comments become parameter descriptions.
/// </remarks>
public static class XmlDocumentationTransformer
{
    private static readonly ConcurrentDictionary<Assembly, Dictionary<string, XElement>> Cache = new();

    /// <summary>Registers the transformer on an OpenAPI document.</summary>
    /// <param name="options">The OpenAPI options being configured.</param>
    public static void AddXmlDocumentation(this OpenApiOptions options)
    {
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor is not ControllerActionDescriptor action)
            {
                return Task.CompletedTask;
            }

            var member = FindMember(action.MethodInfo);
            if (member is null)
            {
                return Task.CompletedTask;
            }

            var summary = Text(member.Element("summary"));
            if (!string.IsNullOrWhiteSpace(summary))
            {
                operation.Summary = summary;
            }

            var remarks = Text(member.Element("remarks"));
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                operation.Description = remarks;
            }

            foreach (var parameter in operation.Parameters ?? [])
            {
                var doc = member.Elements("param")
                    .FirstOrDefault(p => string.Equals(
                        p.Attribute("name")?.Value, parameter.Name, StringComparison.Ordinal));

                var text = Text(doc);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parameter.Description = text;
                }
            }

            return Task.CompletedTask;
        });
    }

    private static XElement? FindMember(MethodInfo method)
    {
        var assembly = method.DeclaringType?.Assembly;
        if (assembly is null)
        {
            return null;
        }

        var members = Cache.GetOrAdd(assembly, Load);

        // Keyed on type and method name only. Overloads would need the full
        // parameter signature; controllers here have none, and a wrong-overload
        // summary is worse than none.
        var key = $"{method.DeclaringType!.FullName}.{method.Name}";
        return members.TryGetValue(key, out var element) ? element : null;
    }

    private static Dictionary<string, XElement> Load(Assembly assembly)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);

        var path = Path.Combine(
            AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var member in XDocument.Load(path).Descendants("member"))
        {
            var name = member.Attribute("name")?.Value;
            if (name is null || !name.StartsWith("M:", StringComparison.Ordinal))
            {
                continue;
            }

            var signature = name[2..];
            var withoutParams = signature.Split('(')[0];
            result.TryAdd(withoutParams, member);
        }

        return result;
    }

    private static string? Text(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        // XML doc comments arrive indented across many lines; collapse to one.
        var raw = string.Concat(element.Nodes().Select(n => n.ToString()));
        var collapsed = string.Join(' ',
            raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
               .Select(line => line.Trim()));

        return collapsed.Length == 0 ? null : collapsed;
    }
}
