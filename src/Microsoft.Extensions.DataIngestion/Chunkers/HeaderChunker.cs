// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// A <see cref="IDocumentChunker"/> that splits documents into chunks based on headers and their corresponding levels, preserving the header context.
/// </summary>
public sealed class HeaderChunker : IDocumentChunker
{
    private const int MaxHeaderLevel = 10;
    private readonly Tokenizer _tokenizer;
    private readonly int _maxTokensPerParagraph;
    private readonly int _overlapTokens;

    public HeaderChunker(Tokenizer tokenizer, int maxTokensPerParagraph, int overlapTokens = 0)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _maxTokensPerParagraph = maxTokensPerParagraph > 0 ? maxTokensPerParagraph : throw new ArgumentOutOfRangeException(nameof(maxTokensPerParagraph));
        _overlapTokens = overlapTokens >= 0 ? overlapTokens : throw new ArgumentOutOfRangeException(nameof(overlapTokens));
    }

    public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        List<DocumentChunk> chunks = new();
        string?[] headers = new string?[MaxHeaderLevel + 1];
        List<string> paragraphs = new();

        foreach (DocumentSection section in document.Sections)
        {
            Process(section, chunks, headers, paragraphs);
        }

        // take care of any remaining paragraphs
        SplitIntoChunks(chunks, headers, paragraphs);

        return Task.FromResult(chunks);
    }

    private void Process(DocumentSection section, List<DocumentChunk> chunks, string?[] headers, List<string> paragraphs)
    {
        foreach (DocumentElement element in section.Elements)
        {
            switch (element)
            {
                case DocumentHeader header:
                    SplitIntoChunks(chunks, headers, paragraphs);

                    int headerLevel = header.Level.GetValueOrDefault();
                    for (int i = headers.Length - 1; i >= headerLevel; i--)
                    {
                        headers[i] = null;
                    }
                    headers[headerLevel] = header.Markdown;
                    break;
                case DocumentSection simple when IsSimpleLeaf(simple):
                    paragraphs.Add(simple.Markdown);
                    break;
                case DocumentSection nestedSection:
                    Process(nestedSection, chunks, headers, paragraphs);
                    break;
                case DocumentImage image:
                    paragraphs.Add(image.AlternativeText ?? image.Text);
                    break;
                case DocumentFooter footer:
                    break;
                default:
                    paragraphs.Add(element.Markdown);
                    break;
            }
        }
    }

    private static bool IsSimpleLeaf(DocumentSection leafSection)
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

    private void SplitIntoChunks(List<DocumentChunk> chunks, string?[] headers, List<string> paragraphs)
    {
        if (paragraphs.Count > 0)
        {
            string chunkHeader = string.Join(" ", headers.Where(h => !string.IsNullOrEmpty(h)));
            foreach (string chunk in TextChunker.SplitMarkdownParagraphs(paragraphs, _maxTokensPerParagraph, _overlapTokens,
                chunkHeader: chunkHeader.Length == 0 ? "" : chunkHeader + ' ', // we need to separate the header from the content
                text => _tokenizer.CountTokens(text)))
            {
                chunks.Add(new DocumentChunk(content: chunk, tokenCount: _tokenizer.CountTokens(chunk), context: chunkHeader));
            }
            paragraphs.Clear();
        }
    }
}
