// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class HeaderChunkerTests
{
    [Fact]
    public async Task CanChunkNonTrivialDocument()
    {
        Document doc = new("nonTrivial");
        doc.Sections.Add(new()
        {
            Elements =
            {
                new DocumentHeader("Header 1") { Level = 1 },
                    new DocumentHeader("Header 1_1") { Level = 2 },
                        new DocumentParagraph("Paragraph 1_1_1"),
                        new DocumentHeader("Header 1_1_1") { Level = 3 },
                            new DocumentParagraph("Paragraph 1_1_1_1"),
                            new DocumentParagraph("Paragraph 1_1_1_2"),
                        new DocumentHeader("Header 1_1_2") { Level = 3 },
                            new DocumentParagraph("Paragraph 1_1_2_1"),
                            new DocumentParagraph("Paragraph 1_1_2_2"),
                    new DocumentHeader("Header 1_2") { Level = 2 },
                        new DocumentParagraph("Paragraph 1_2_1"),
                        new DocumentHeader("Header 1_2_1") { Level = 3 },
                            new DocumentParagraph("Paragraph 1_2_1_1"),
            }
        });

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"));
        List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

        Assert.Equal(5, chunks.Count);
        string nl = Environment.NewLine;
        Assert.Equal("Header 1 Header 1_1", chunks[0].Context);
        Assert.Equal($"Header 1 Header 1_1{nl}Paragraph 1_1_1", chunks[0].Content);
        Assert.Equal("Header 1 Header 1_1 Header 1_1_1", chunks[1].Context);
        Assert.Equal($"Header 1 Header 1_1 Header 1_1_1{nl}Paragraph 1_1_1_1{nl}Paragraph 1_1_1_2", chunks[1].Content);
        Assert.Equal("Header 1 Header 1_1 Header 1_1_2", chunks[2].Context);
        Assert.Equal($"Header 1 Header 1_1 Header 1_1_2{nl}Paragraph 1_1_2_1{nl}Paragraph 1_1_2_2", chunks[2].Content);
        Assert.Equal("Header 1 Header 1_2", chunks[3].Context);
        Assert.Equal($"Header 1 Header 1_2{nl}Paragraph 1_2_1", chunks[3].Content);
        Assert.Equal("Header 1 Header 1_2 Header 1_2_1", chunks[4].Context);
        Assert.Equal($"Header 1 Header 1_2 Header 1_2_1{nl}Paragraph 1_2_1_1", chunks[4].Content);
    }

    [Fact]
    public async Task CanRespectTokenLimit()
    {
        Document doc = new("longOne");
        doc.Sections.Add(new()
        {
            Elements =
            {
                new DocumentHeader("Header A") { Level = 1 },
                    new DocumentHeader("Header B") { Level = 2 },
                        new DocumentHeader("Header C") { Level = 3 },
                            new DocumentParagraph("This is a very long text. It's expressed with plenty of tokens")
            }
        });

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 13);
        List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Header A Header B Header C", chunks[0].Context);
        Assert.Equal($"Header A Header B Header C\nThis is a very long text.", chunks[0].Content, ignoreLineEndingDifferences: true);
        Assert.Equal("Header A Header B Header C", chunks[1].Context);
        Assert.Equal($"Header A Header B Header C\n It's expressed with plenty of tokens", chunks[1].Content, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task ThrowsWhenLimitIsTooLowToFitAnythingMoreThanContext()
    {
        Document doc = new("longOne");
        doc.Sections.Add(new()
        {
            Elements =
            {
                new DocumentHeader("Header A") { Level = 1 }, // 2 tokens
                    new DocumentHeader("Header B") { Level = 2 }, // 2 tokens
                        new DocumentHeader("Header C") { Level = 3 }, // 2 tokens
                            new DocumentParagraph("This is a very long text. It's expressed with plenty of tokens")
            }
        });

        HeaderChunker lessThanContext = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 5);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lessThanContext.ProcessAsync(doc));

        HeaderChunker sameAsContext = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 6);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sameAsContext.ProcessAsync(doc));
    }

    [Fact]
    public async Task CanSplitLongerParagraphsOnNewLine()
    {
        Document doc = new("withNewLines");
        doc.Sections.Add(new()
        {
            Elements =
            {
                new DocumentHeader("Header A") { Level = 1 },
                    new DocumentHeader("Header B") { Level = 2 },
                        new DocumentHeader("Header C") { Level = 3 },
                            new DocumentParagraph(
@"This is a very long text. It's expressed with plenty of tokens. And it contains a new line.
With some text after the new line."),
                            new DocumentParagraph("And following paragraph.")
            }
        });

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 30);
        List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Header A Header B Header C", chunks[0].Context);
        Assert.Equal($"Header A Header B Header C\nThis is a very long text. It's expressed with plenty of tokens. And it contains a new line.\n",
            chunks[0].Content, ignoreLineEndingDifferences: true);
        Assert.Equal("Header A Header B Header C", chunks[1].Context);
        Assert.Equal($"Header A Header B Header C\nWith some text after the new line.\nAnd following paragraph.", chunks[1].Content, ignoreLineEndingDifferences: true);
    }

    // We need plenty of more tests here, especially for edge cases:
    // - sentence splitting
    // - markdown splitting (e.g. lists, code blocks, tables, etc.)
}
