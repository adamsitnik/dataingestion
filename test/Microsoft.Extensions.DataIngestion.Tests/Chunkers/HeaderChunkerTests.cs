// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests;

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

    [Fact]
    public async Task ThrowsWhenHeaderSeparatorAndSingleRowExceedTokenLimit()
    {
        Document document = CreateDocumentWithLargeTable();

        // It takes 38 tokens to represent Headers, Separator and the first Row.
        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 37);

        await Assert.ThrowsAsync<InvalidOperationException>(() => chunker.ProcessAsync(document));
    }

    [Fact]
    public async Task CanSplitLargeTableIntoMultipleChunks_MultipleRowsPerChunk()
    {
        Document document = CreateDocumentWithLargeTable();

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 100);
        List<DocumentChunk> chunks = await chunker.ProcessAsync(document);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal("Header A", chunk.Context));
        Assert.Equal(
@"Header A
This is some text that describes why we need the following table.
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 0 | 1 | 2 | 3 | 4 |
| 5 | 6 | 7 | 8 | 9 |
| 10 | 11 | 12 | 13 | 14 |", chunks[0].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 15 | 16 | 17 | 18 | 19 |
| 20 | 21 | 22 | 23 | 24 |
And some follow up.", chunks[1].Content, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task CanSplitLargeTableIntoMultipleChunks_OneRowPerChunk()
    {
        Document document = CreateDocumentWithLargeTable();

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), maxTokensPerChunk: 50);
        List<DocumentChunk> chunks = await chunker.ProcessAsync(document);

        Assert.Equal(6, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal("Header A", chunk.Context));
        Assert.All(chunks, chunk => Assert.InRange(chunk.TokenCount.GetValueOrDefault(), 1, 50));

        Assert.Equal(
@"Header A
This is some text that describes why we need the following table.", chunks[0].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 0 | 1 | 2 | 3 | 4 |", chunks[1].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 5 | 6 | 7 | 8 | 9 |", chunks[2].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 10 | 11 | 12 | 13 | 14 |", chunks[3].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 15 | 16 | 17 | 18 | 19 |", chunks[4].Content, ignoreLineEndingDifferences: true);
        Assert.Equal(
@"Header A
| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 20 | 21 | 22 | 23 | 24 |
And some follow up.", chunks[5].Content, ignoreLineEndingDifferences: true);
    }

    private static Document CreateDocumentWithLargeTable()
    {
        DocumentTable table = new(
@"| one | two | three | four | five |
| --- | --- | --- | --- | --- |
| 0 | 1 | 2 | 3 | 4 |
| 5 | 6 | 7 | 8 | 9 |
| 10 | 11 | 12 | 13 | 14 |
| 15 | 16 | 17 | 18 | 19 |
| 20 | 21 | 22 | 23 | 24 |",
    CreateTableCells()
);

        Document doc = new("withNewLines");
        doc.Sections.Add(new()
        {
            Elements =
            {
                new DocumentHeader("Header A") { Level = 1 },
                    new DocumentParagraph("This is some text that describes why we need the following table."),
                    table,
                    new DocumentParagraph("And some follow up.")
            }
        });

        return doc;

        static string[,] CreateTableCells()
        {
            string[,] cells = new string[6, 5]; // 6 rows (1 header + 5 data rows), 5 columns

            // Header row
            cells[0, 0] = "one";
            cells[0, 1] = "two";
            cells[0, 2] = "three";
            cells[0, 3] = "four";
            cells[0, 4] = "five";

            // Data rows (0-29)
            int number = 0;
            for (int row = 1; row <= 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    cells[row, col] = number.ToString();
                    number++;
                }
            }

            return cells;
        }
    }

    // We need plenty of more tests here, especially for edge cases:
    // - sentence splitting
    // - markdown splitting (e.g. lists, code blocks etc.)
}
