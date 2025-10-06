// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers;

/// <summary>
/// Treats each section in a <see cref="Document"/> as a separate entity.
/// </summary>
public sealed class SectionChunker : IDocumentChunker
{
    private readonly ElementsChunker _elementsChunker;

    public SectionChunker(Tokenizer tokenizer, int maxTokensPerChunk = 2_000, int chunkOverlap = 500)
        => _elementsChunker = new(tokenizer, maxTokensPerChunk, chunkOverlap);

    public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<DocumentChunk> chunks = [];
        foreach (DocumentSection section in document.Sections)
        {
            Process(section, chunks);
        }

        return Task.FromResult(chunks);
    }

    private void Process(DocumentSection section, List<DocumentChunk> chunks, string? parentContext = null)
    {
        List<DocumentElement> elements = new(section.Elements.Count);
        string context = parentContext ?? string.Empty;

        for (int i = 0; i < section.Elements.Count; i++)
        {
            switch (section.Elements[i])
            {
                // If the first element is a header, we use it as a context.
                // This is common for various documents and readers.
                case DocumentHeader documentHeader when i == 0:
                    context = string.IsNullOrEmpty(context)
                        ? documentHeader.Markdown
                        : context + $" {documentHeader.Markdown}";
                break;
                case DocumentSection nestedSection:
                    Commit();
                    Process(nestedSection, chunks, context);
                    break;
                default:
                    elements.Add(section.Elements[i]);
                    break;
            }
        }

        Commit();

        void Commit()
        {
            if (elements.Count > 0)
            {
                _elementsChunker.Process(chunks, context, elements);
                elements.Clear();
            }
        }
    }
}
