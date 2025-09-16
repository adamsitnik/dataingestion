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
        doc.Sections.Add(new DocumentSection
        {
            Markdown = "first section",
            Elements =
            {
                new DocumentHeader { Markdown = "Header 1", Level = 1 },
                    new DocumentHeader { Markdown = "Header 1_1", Level = 2 },
                        new DocumentParagraph { Markdown = "Paragraph 1_1_1" },
                        new DocumentHeader { Markdown = "Header 1_1_1", Level = 3 },
                            new DocumentParagraph { Markdown = "Paragraph 1_1_1_1" },
                            new DocumentParagraph { Markdown = "Paragraph 1_1_1_2" },
                        new DocumentHeader { Markdown = "Header 1_1_2", Level = 3 },
                            new DocumentParagraph { Markdown = "Paragraph 1_1_2_1" },
                            new DocumentParagraph { Markdown = "Paragraph 1_1_2_2" },
                    new DocumentHeader { Markdown = "Header 1_2", Level = 2 },
                        new DocumentParagraph { Markdown = "Paragraph 1_2_1" },
                        new DocumentHeader { Markdown = "Header 1_2_1", Level = 3 },
                            new DocumentParagraph { Markdown = "Paragraph 1_2_1_1" },
            }
        });

        HeaderChunker chunker = new(TiktokenTokenizer.CreateForModel("gpt-4"), 2000, 500);
        List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

        Assert.Equal(5, chunks.Count);
        string nl = Environment.NewLine;
        Assert.Equal("Header 1 Header 1_1", chunks[0].Context);
        Assert.Equal("Header 1 Header 1_1 Paragraph 1_1_1", chunks[0].Content);
        Assert.Equal("Header 1 Header 1_1 Header 1_1_1", chunks[1].Context);
        Assert.Equal($"Header 1 Header 1_1 Header 1_1_1 Paragraph 1_1_1_1{nl}Paragraph 1_1_1_2", chunks[1].Content);
        Assert.Equal("Header 1 Header 1_1 Header 1_1_2", chunks[2].Context);
        Assert.Equal($"Header 1 Header 1_1 Header 1_1_2 Paragraph 1_1_2_1{nl}Paragraph 1_1_2_2", chunks[2].Content);
        Assert.Equal("Header 1 Header 1_2", chunks[3].Context);
        Assert.Equal("Header 1 Header 1_2 Paragraph 1_2_1", chunks[3].Content);
        Assert.Equal("Header 1 Header 1_2 Header 1_2_1", chunks[4].Context);
        Assert.Equal("Header 1 Header 1_2 Header 1_2_1 Paragraph 1_2_1_1", chunks[4].Content);
    }
}
