// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.DataIngestion.Chunkers;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class DocumentTokenChunkerTests : DocumentChunkerTests
    {
        protected override DocumentChunker CreateDocumentChunker()
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            int chunkSize = 512;
            int chunkOverlap = 128;
            return new DocumentTokenChunker(tokenizer, chunkSize, chunkOverlap);
        }

        private DocumentTokenChunker CreateNoOverlapTokenChkunker()
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            int chunkSize = 512;
            int chunkOverlap = 0;
            return new DocumentTokenChunker(tokenizer, chunkSize, chunkOverlap);
        }

        [Fact]
        public async Task SingleChunkText()
        {
            string text = "This is a short document that fits within a single chunk.";
            Document doc = new Document("singleChunkDoc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph { Markdown = text }
                }
            });

            DocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            DocumentChunk chunk = chunks.First();
            ChunkAssertions.ContentEquals(text, chunk);
        }

        [Fact]
        public async Task TwoChunks_NoOverlap()
        {
            string text = string.Join(" ", Enumerable.Repeat("word", 600)); // 600 words
            Document doc = new Document("twoChunksNoOverlapDoc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph { Markdown = text }
                }
            });
            DocumentChunker chunker = CreateNoOverlapTokenChkunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            Assert.True(chunks[0].Content.Split(' ').Length <= 512);
            Assert.True(chunks[1].Content.Split(' ').Length <= 512);
            Assert.Equal(text, string.Join("", chunks.Select(c => c.Content)));
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
                    new DocumentParagraph { Markdown = text }
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
