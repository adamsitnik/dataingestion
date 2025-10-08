// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers;

/// <summary>
/// Treats each section in a <see cref="IngestionDocument"/> as a separate entity.
/// </summary>
public sealed class SectionChunker : IngestionChunker
{
    private readonly ElementsChunker _elementsChunker;

    public SectionChunker(Tokenizer tokenizer, IngestionChunkerOptions? options = default)
        => _elementsChunker = new(tokenizer, options ?? new());

    public Task<List<IngestionChunk>> ProcessAsync(IngestionDocument document, CancellationToken cancellationToken = default)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<IngestionChunk> chunks = [];
        foreach (IngestionDocumentSection section in document.Sections)
        {
            Process(document, section, chunks);
        }

        return Task.FromResult(chunks);
    }

    private void Process(IngestionDocument document, IngestionDocumentSection section, List<IngestionChunk> chunks, string? parentContext = null)
    {
        List<IngestionDocumentElement> elements = new(section.Elements.Count);
        string context = parentContext ?? string.Empty;

        for (int i = 0; i < section.Elements.Count; i++)
        {
            switch (section.Elements[i])
            {
                // If the first element is a header, we use it as a context.
                // This is common for various documents and readers.
                case IngestionDocumentHeader documentHeader when i == 0:
                    context = string.IsNullOrEmpty(context)
                        ? documentHeader.GetMarkdown()
                        : context + $" {documentHeader.GetMarkdown()}";
                break;
                case IngestionDocumentSection nestedSection:
                    Commit();
                    Process(document, nestedSection, chunks, context);
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
                _elementsChunker.Process(document, chunks, context, elements);
                elements.Clear();
            }
        }
    }
}
