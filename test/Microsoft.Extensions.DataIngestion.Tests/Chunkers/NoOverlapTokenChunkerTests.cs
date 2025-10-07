// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class NoOverlapDocumentTokenChunkerTests : DocumentTokenChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new DocumentTokenChunker(tokenizer, new() { MaxTokensPerChunk = maxTokensPerChunk, OverlapTokens = 0 });
        }

        [Fact]
        public async Task TwoChunks()
        {
            string text = string.Join(" ", Enumerable.Repeat("word", 600)); // each word is 1 token
            Document doc = new Document("twoChunksNoOverlapDoc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph(text)
                }
            });
            IDocumentChunker chunker = CreateDocumentChunker(maxTokensPerChunk: 512);
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            Assert.True(chunks[0].Content.Split(' ').Length <= 512);
            Assert.True(chunks[1].Content.Split(' ').Length <= 512);
            Assert.Equal(text, string.Join("", chunks.Select(c => c.Content)));
        }
    }
}
