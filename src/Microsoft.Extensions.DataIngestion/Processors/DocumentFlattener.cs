// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Flattens the Document structure and represents every Document as a single section containing all elements.
/// </summary>
public sealed class DocumentFlattener : IDocumentProcessor
{
    public Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        // Since we have a single section that contains all elements,
        // we can treat the Markdown of the whole Document as the section's Markdown.
        DocumentSection rootSection = new(document.Markdown);

        rootSection.Elements.AddRange(document);

        Document flat = new(document.Identifier)
        {
            Markdown = document.Markdown, // Markdown needs to be preserved
            Sections = { rootSection }
        };

        return Task.FromResult(flat);
    }
}
