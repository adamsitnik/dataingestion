// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text;

namespace Microsoft.Extensions.DataIngestion
{
    internal static class ElementUtils
    {
        internal static string GetSemanticContent(DocumentElement documentElement)
        {
            switch (documentElement)
            {
                case DocumentImage image:
                    return image.AlternativeText ?? image.Text;
                case DocumentFooter footer:
                    return string.Empty; // Footers are typically not relevant for semantic content
                case DocumentSection simple when IsSimpleLeaf(simple):
                    return simple.Markdown;
                case DocumentSection nestedSection:
                    StringBuilder result = new();
                    foreach (var element in nestedSection.Elements)
                    {
                        result.Append(GetSemanticContent(element));
                    }
                    return result.ToString();
                default:
                    return documentElement.Markdown;
            }
        }

        internal static bool IsSimpleLeaf(DocumentSection leafSection)
        {
            foreach (DocumentElement element in leafSection.Elements)
            {
                if (element is not DocumentParagraph)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
