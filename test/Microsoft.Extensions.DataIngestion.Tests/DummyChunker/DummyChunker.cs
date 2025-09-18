// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DummyChunker : IDocumentChunker
{
    public ValueTask<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));

        List<DocumentChunk> chunks = new();
        foreach (DocumentSection section in document.Sections)
        {
            Add(section, chunks);
        }
        return new(chunks);
    }

    private static void Add(DocumentSection section, List<DocumentChunk> chunks)
    {
        foreach (DocumentElement element in section.Elements)
        {
            switch (element)
            {
                case DocumentSection nested:
                    Add(nested, chunks); // Recursively add nested sections
                    break;
                case DocumentFooter footer:
                    break; // We don't care about footers (they usually contain page numbers or similar)
                default:
                    if (!string.IsNullOrEmpty(element.Markdown))
                    {
                        chunks.Add(new DocumentChunk(element.Markdown));
                    }
                    break;
            }
        }
    }
}
