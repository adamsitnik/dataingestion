// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public class DocumentFlattener : IDocumentProcessor
{
    public ValueTask<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
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

        rootSection.Elements.AddRange(document.Where(element => element is not DocumentSection));

        Document flat = new(document.Identifier)
        {
            Markdown = document.Markdown, // Markdown needs to be preserved
            Sections = { rootSection }
        };

        return new(flat);
    }
}
