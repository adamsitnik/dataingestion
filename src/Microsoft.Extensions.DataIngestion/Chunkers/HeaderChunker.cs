// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Splits documents into chunks based on headers and their corresponding levels, preserving the header context.
/// </summary>
public sealed class HeaderChunker : IngestionChunker
{
    private const int MaxHeaderLevel = 10;
    private readonly ElementsChunker _elementsChunker;

    public HeaderChunker(Tokenizer tokenizer, IngestionChunkerOptions? options = default)
        => _elementsChunker = new(tokenizer, options ?? new());

    public Task<List<IngestionChunk>> ProcessAsync(IngestionDocument document, CancellationToken cancellationToken = default)
    {
        List<IngestionChunk> chunks = new();
        List<IngestionDocumentElement> elements = new(20);
        string?[] headers = new string?[MaxHeaderLevel + 1];

        foreach (IngestionDocumentElement element in document.EnumerateContent())
        {
            if (element is IngestionDocumentHeader header)
            {
                SplitIntoChunks(document, chunks, headers, elements);

                int headerLevel = header.Level.GetValueOrDefault();
                headers[headerLevel] = header.GetMarkdown();
                headers.AsSpan(headerLevel + 1).Clear(); // clear all lower level headers

                continue; // don't add headers to the elements list, they are part of the context
            }

            elements.Add(element);
        }

        // take care of any remaining paragraphs
        SplitIntoChunks(document, chunks, headers, elements);

        return Task.FromResult(chunks);
    }

    private void SplitIntoChunks(IngestionDocument document, List<IngestionChunk> chunks, string?[] headers, List<IngestionDocumentElement> elements)
    {
        if (elements.Count > 0)
        {
            string chunkHeader = string.Join(" ", headers.Where(h => !string.IsNullOrEmpty(h)));

            _elementsChunker.Process(document, chunks, chunkHeader, elements);

            elements.Clear();
        }
    }
}
