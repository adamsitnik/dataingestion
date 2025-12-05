// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public abstract class BasicSemanticTests : DocumentChunkerTests
    {
        [Fact]
        public async Task SingleParagraph()
        {
            string text = ".NET is a free, cross-platform, open-source developer platform for building many kinds of applications. It can run programs written in multiple languages, with C# being the most popular. It relies on a high-performance runtime that is used in production by many high-scale apps.";
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph(text)
                }
            });
            IngestionChunker<string> chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();
            Assert.Single(chunks);
            Assert.Equal(text, chunks[0].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task TwoTopicsParagraphs()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            string text1 = ".NET is a free, cross-platform, open-source developer platform for building many kinds of applications. It can run programs written in multiple languages, with C# being the most popular.";
            string text2 = "It relies on a high-performance runtime that is used in production by many high-scale apps.";
            string text3 = "Zeus is the chief deity of the Greek pantheon. He is a sky and thunder god in ancient Greek religion and mythology.";
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph(text1),
                    new IngestionDocumentParagraph(text2),
                    new IngestionDocumentParagraph(text3)
                }
            });

            IngestionChunker<string> chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();
            Assert.Equal(2, chunks.Count);
            Assert.Equal(text1 + Environment.NewLine + text2, chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(text3, chunks[1].Content, ignoreLineEndingDifferences: true);
        }
    }
}
