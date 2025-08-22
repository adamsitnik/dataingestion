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

public class ParagraphChunker : DocumentChunker
{
    private readonly Tokenizer _tokenizer;
    private readonly int _maxTokensPerParagraph;
    private readonly int _overlapTokens;

    public ParagraphChunker(Tokenizer tokenizer, int maxTokensPerParagraph, int overlapTokens = 0)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _maxTokensPerParagraph = maxTokensPerParagraph > 0 ? maxTokensPerParagraph : throw new ArgumentOutOfRangeException(nameof(maxTokensPerParagraph));
        _overlapTokens = overlapTokens >= 0 ? overlapTokens : throw new ArgumentOutOfRangeException(nameof(overlapTokens));
    }

    public override ValueTask<List<Chunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        List<Chunk> chunks = new();
        List<string?> headers = new();
        List<string> paragraphs = new();

        foreach (DocumentSection section in document.Sections)
        {
            Process(section, chunks, headers, paragraphs);
        }

        return new(chunks);
    }

    private void Process(DocumentSection section, List<Chunk> chunks, List<string?> headers, List<string> paragraphs)
    {
        string? chunkHeader = null;
        foreach (DocumentElement element in section.Elements)
        {
            switch (element)
            {
                case DocumentHeader header:
                    if (paragraphs.Count > 0)
                    {
                        chunkHeader ??= string.Join(" ", headers.Where(h => !string.IsNullOrEmpty(h)));
                        foreach (string chunk in TextChunker.SplitPlainTextParagraphs(paragraphs, _maxTokensPerParagraph, _overlapTokens, chunkHeader,
                            text => _tokenizer.CountTokens(text)))
                        {
                            chunks.Add(new Chunk(chunk, tokenCount: _tokenizer.CountTokens(chunk)));
                        }
                        paragraphs.Clear();
                    }

                    int headerLevel = header.Level.GetValueOrDefault();
                    for (int i = headers.Count - 1; i > headerLevel; i--)
                    {
                        headers.RemoveAt(i);
                    }
                    headers.Insert(headerLevel, header.Text);
                    break;
                case DocumentSection simple when IsSimpleLeaf(simple):
                    paragraphs.Add(simple.Markdown);
                    break;
                case DocumentSection nestedSection:
                    Process(nestedSection, chunks, headers, paragraphs);
                    break;
                case DocumentImage image:
                    paragraphs.Add(image.Description ?? image.Text);
                    break;
                case DocumentFooter footer:
                    break;
                default:
                    paragraphs.Add(element.Markdown);
                    break;
            }
        }
    }

    private bool IsSimpleLeaf(DocumentSection leafSection)
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
}
