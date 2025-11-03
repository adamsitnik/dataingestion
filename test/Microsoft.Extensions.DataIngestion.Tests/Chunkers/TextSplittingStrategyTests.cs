// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public abstract class TextSplittingStrategyTests
    {
        protected  StrategyChunker GetDelimiterStrategyChunker(int maxTokenCount = 50)
        {
            TextSplittingStrategy strategy = GetTextSplittingStrategy();
            return new StrategyChunker(strategy, maxTokenCount);
        }
        protected abstract TextSplittingStrategy GetTextSplittingStrategy();

        [Fact]
        public async Task SingleShortParagraph()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph("This is a short paragraph.")
                }
            });
            IngestionChunker<string> chunker = GetDelimiterStrategyChunker();
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();
            Assert.Single(chunks);
            Assert.Equal("This is a short paragraph.", chunks[0].Content);
        }

        [Fact]
        public async Task EmptyString()
        {
            TextSplittingStrategy textSplittingStrategy = GetTextSplittingStrategy();
            List<int> indices = textSplittingStrategy.GetSplitIndices(string.Empty, 50);
            Assert.Empty(indices);
        }
    }
}
