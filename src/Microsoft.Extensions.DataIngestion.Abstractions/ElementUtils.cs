// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    public static class ElementUtils
    {
        public static string GetSemanticContent(DocumentElement element)
        {
            switch (element)
            {
                case DocumentImage image:
                    return image.AlternativeText ?? image.Text;
                case DocumentFooter footer:
                    return string.Empty; // Footers are typically not relevant for semantic content
                case DocumentSection simple when IsSimpleLeaf(simple):
                    return simple.Markdown;
                case DocumentSection nestedSection:
                    Document doc = new("");
                    doc.Sections.Add(nestedSection);

                    return string.Join(" ", doc.Where(element => element is not DocumentSection).Select(GetSemanticContent));
                default:
                    return element.Markdown;
            }
        }

        public static bool IsSimpleLeaf(DocumentSection leafSection)
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
