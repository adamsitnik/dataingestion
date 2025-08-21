// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public class DocumentFlattener : DocumentProcessor
{
    public override ValueTask<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        DocumentSection rootSection = new()
        {
            // Since we have a single section that contains all elements,
            // we can treat the Markdown of the whole Document as the section's Markdown.
            Markdown = document.Markdown,
        };

        FlattenAndKeepOrder(document.Sections, rootSection.Elements);

        Document flat = new(document.Identifier)
        {
            Markdown = document.Markdown, // Markdown needs to be preserved
            Sections = { rootSection }
        };

        return new(flat);
    }

    private static void FlattenAndKeepOrder(List<DocumentSection> sections, List<DocumentElement> targetElements)
    {
        Stack<DocumentElement> elementsToProcess = new();
        
        for (int sectionIndex = sections.Count - 1; sectionIndex >= 0; sectionIndex--)
        {
            DocumentSection section = sections[sectionIndex];
            for (int elementIndex = section.Elements.Count - 1; elementIndex >= 0; elementIndex--)
            {
                elementsToProcess.Push(section.Elements[elementIndex]);
            }
        }

        while (elementsToProcess.Count > 0)
        {
            DocumentElement currentElement = elementsToProcess.Pop();
            
            if (currentElement is DocumentSection nestedSection)
            {
                for (int i = nestedSection.Elements.Count - 1; i >= 0; i--)
                {
                    elementsToProcess.Push(nestedSection.Elements[i]);
                }
            }
            else
            {
                targetElements.Add(currentElement);
            }
        }
    }
}
