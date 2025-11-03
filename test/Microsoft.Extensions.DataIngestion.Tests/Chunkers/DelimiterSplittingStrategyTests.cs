// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class DelimiterSplittingStrategyTests : TextSplittingStrategyTests
    {
        protected override TextSplittingStrategy GetTextSplittingStrategy()
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new DelimiterSplittingStrategy(tokenizer, '\n');
        }

    [Fact]
        public async Task TwoShortParagraphs()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
            {
                new IngestionDocumentParagraph("This is the first short paragraph."),
                new IngestionDocumentParagraph("This is the second short paragraph.")
            }
            });
            IngestionChunker<string> chunker = GetDelimiterStrategyChunker(200);
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();
            Assert.Single(chunks);
            string expectedContent = "This is the first short paragraph.This is the second short paragraph.";
            Assert.Equal(expectedContent, chunks[0].Content);
        }
    }
}
