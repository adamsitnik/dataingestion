// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DummyChunker : DocumentChunker
{
    private const int DummyTokenCount = 1;

    public override ValueTask<List<Chunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        List<Chunk> chunks = new();
        foreach (DocumentSection section in document.Sections)
        {
            Add(section, chunks);
        }
        return new(chunks);
    }

    private void Add(DocumentSection section, List<Chunk> chunks)
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
                    chunks.Add(new Chunk(element.Markdown, tokenCount: DummyTokenCount));
                    break;
            }
        }
    }
}
