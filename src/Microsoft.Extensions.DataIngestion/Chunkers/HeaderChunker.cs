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
public sealed class HeaderChunker : IDocumentChunker
{
    private const int MaxHeaderLevel = 10;
    private readonly ElementsChunker _elementsChunker;

    public HeaderChunker(Tokenizer tokenizer, int maxTokensPerChunk = 2_000, int overlapTokens = 0)
        => _elementsChunker = new(tokenizer, maxTokensPerChunk, overlapTokens);

    public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        List<DocumentChunk> chunks = new();
        List<DocumentElement> elements = new(20);
        string?[] headers = new string?[MaxHeaderLevel + 1];

        foreach (DocumentElement element in document)
        {
            if (element is DocumentHeader header)
            {
                SplitIntoChunks(chunks, headers, elements);

                int headerLevel = header.Level.GetValueOrDefault();
                headers[headerLevel] = header.Markdown;
                headers.AsSpan(headerLevel + 1).Clear(); // clear all lower level headers

                continue; // don't add headers to the elements list, they are part of the context
            }

            elements.Add(element);
        }

        // take care of any remaining paragraphs
        SplitIntoChunks(chunks, headers, elements);

        return Task.FromResult(chunks);
    }

    private void SplitIntoChunks(List<DocumentChunk> chunks, string?[] headers, List<DocumentElement> elements)
    {
        if (elements.Count > 0)
        {
            string chunkHeader = string.Join(" ", headers.Where(h => !string.IsNullOrEmpty(h)));

            _elementsChunker.Process(chunks, chunkHeader, elements);

            elements.Clear();
        }
    }
}
