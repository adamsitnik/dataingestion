// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class OverlapTokenChunkerTests : DocumentTokenChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new DocumentTokenChunker(tokenizer, maxTokensPerChunk, overlapTokens);
        }

        [Fact]
        public async Task TokenChunking_WithOverlap()
        {
            string text = "The quick brown fox jumps over the lazy dog";
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            int chunkSize = 4;  // Small chunk size to demonstrate overlap
            int chunkOverlap = 1;

            var chunker = new DocumentTokenChunker(tokenizer, chunkSize, chunkOverlap);
            Document doc = new Document("overlapExample");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph(text)
                }
            });

            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(3, chunks.Count);
            ChunkAssertions.ContentEquals("The quick brown fox", chunks[0]);
            ChunkAssertions.ContentEquals("fox jumps over the", chunks[1]);
            ChunkAssertions.ContentEquals("the lazy dog", chunks[2]);

            Assert.True(chunks.Last().TokenCount <= chunkSize);

            for (int i = 0; i < chunks.Count - 1; i++)
            {
                var currentChunk = chunks[i];
                var nextChunk = chunks[i + 1];

                var currentWords = currentChunk.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var nextWords = nextChunk.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                bool hasOverlap = currentWords.Intersect(nextWords).Any();
                Assert.True(hasOverlap, $"Chunks {i} and {i + 1} should have overlapping content");
            }
            Assert.NotEmpty(string.Concat(chunks.Select(c => c.Content)));
        }
    }
}
