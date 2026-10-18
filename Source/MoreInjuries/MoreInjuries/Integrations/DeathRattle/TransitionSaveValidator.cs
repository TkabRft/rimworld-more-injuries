using System;
using System.Collections.Generic;
using System.Xml;

namespace MoreInjuries.Integrations.DeathRattle;

// Pure XML operations shared by runtime save validation and fixture tests.
internal static class TransitionSaveValidator
{
    internal static string? FindResidual(XmlDocument document, ISet<string> sourceIdentifiers)
    {
        foreach (XmlElement element in document.SelectNodes("//*")!)
        {
            if (element.GetAttribute("Class").StartsWith("DeathRattle.", StringComparison.Ordinal))
            {
                return element.GetAttribute("Class");
            }
            if (element.ChildNodes.Count == 1 && element.FirstChild is XmlText && sourceIdentifiers.Contains(element.InnerText))
            {
                return element.Name + "=" + element.InnerText;
            }
        }
        return null;
    }

    internal static void RemapReferences(XmlDocument document, IReadOnlyDictionary<string, string> replacements)
    {
        foreach (XmlElement element in document.SelectNodes("//*")!)
        {
            if (element.ChildNodes.Count == 1 && element.FirstChild is XmlText
                && replacements.TryGetValue(element.InnerText, out string replacement)) { element.InnerText = replacement; }
        }
    }

    internal static void RemoveDependency(XmlDocument document)
    {
        XmlNode? meta = document.SelectSingleNode("/*/meta");
        XmlNodeList? ids = meta?.SelectNodes("modIds/li");
        if (ids is null) { throw new InvalidOperationException("Missing native save mod metadata."); }
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            string id = ids[i]!.InnerText;
            if (!string.Equals(id, "Troopersmith1.DeathRattle", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(id, "Troopersmith1.DeathRattle_steam", StringComparison.OrdinalIgnoreCase)) { continue; }
            foreach (string listName in new[] { "modNames", "modSteamIds" })
            {
                XmlNodeList? entries = meta!.SelectNodes(listName + "/li");
                if (entries is null || entries.Count == 0) { continue; }
                if (entries.Count != ids.Count) { throw new InvalidOperationException("Misaligned native save mod metadata."); }
                entries[i]!.ParentNode!.RemoveChild(entries[i]!);
            }
            ids[i]!.ParentNode!.RemoveChild(ids[i]!);
        }
    }
}
