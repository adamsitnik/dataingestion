// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DataIngestion.Chunkers;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class MarkdownChunkerTests : DocumentChunkerTests
    {
        override protected DocumentChunker CreateDocumentChunker()
        {
            return new MarkdownChunker();
        }

        

        [Fact]
        public async Task NoheaderDocument()
        {
            Document noHeaerDoc = new Document("noHeaderDoc");
            noHeaerDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph
                    {
                        Markdown = "This is a document without headers.".ReplaceLineEndings()
                    }
                }
            });

            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(noHeaerDoc);
            Assert.True(chunks.Count == 1);

            Chunk chunk = chunks.First();
            ChunkAssertions.ContextEquals(string.Empty, chunk);
            ChunkAssertions.ContentEquals("This is a document without headers.", chunk);
        }

        [Fact]
        public async Task SingleHeaderDocument()
        {
            Document singleHeaderDoc = new Document("singleHeaderDoc");
            singleHeaderDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = "This is the content under header 1.".ReplaceLineEndings()
                    }
                }
            });

            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(singleHeaderDoc);
            Assert.True(chunks.Count == 1);
            Chunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            ChunkAssertions.ContentEquals("This is the content under header 1.", chunk);
        }

        [Fact]
        public async Task SingleHeaderTwoParagraphDocument()
        {
            string content = "This is the first paragraph.\n\nThis is the second paragraph.".ReplaceLineEndings();
            Document singleHeaderTwoParagraphDoc = new Document("singleHeaderTwoParagraphDoc")
            {
                Markdown = "# Header 1\n" + content
            };
            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(singleHeaderTwoParagraphDoc);
            Assert.True(chunks.Count == 1);
            Chunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            ChunkAssertions.ContentEquals(content, chunk);
        }

        [Fact]
        public async Task MultiHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            Document multiHeaderDoc = new Document("multiHeaderDoc")
            {
                Markdown = "# Header 1\n" + content1 + "\n## Header 2\n" + content2
            };
            DocumentChunker chunker = new MarkdownChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(multiHeaderDoc);
            Assert.True(chunks.Count == 2);

            Chunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            Chunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);
        }

        [Fact]
        public async Task TwoHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            Document twoHeaderDoc = new Document("twoHeaderDoc")
            {
                Markdown = "# Header 1\n" + content1 + "\n# Header 2\n" + content2
            };
            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(twoHeaderDoc);
            Assert.True(chunks.Count == 2);

            Chunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            Chunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);
        }

        [Fact]
        public async Task ComplexDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            string content3 = "This is the content under header 3.".ReplaceLineEndings();
            string content4 = "This is the content under header 4.".ReplaceLineEndings();
            Document complexDoc = new Document("complexDoc")
            {
                Markdown = "# Header 1\n" + content1 +
                           "\n## Header 2\n" + content2 +
                           "\n### Header 3\n" + content3 +
                           "\n## Header 4\n" + content4
            };
            DocumentChunker chunker = CreateDocumentChunker();

            List<Chunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(4, chunks.Count);

            Chunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            Chunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);

            Chunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2;### Header 3", chunk3);
            ChunkAssertions.ContentEquals(content3, chunk3);

            Chunk chunk4 = chunks[3];
            ChunkAssertions.ContextEquals("# Header 1;## Header 4", chunk4);
            ChunkAssertions.ContentEquals(content4, chunk4);
        }

        [Fact]
        public async Task ComplexDocument_SplitOnLowerLevel()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            string content3 = "This is the content under header 3.".ReplaceLineEndings();
            string content4 = "This is the content under header 4.".ReplaceLineEndings();
            Document complexDoc = new Document("complexDoc")
            {
                Markdown = "# Header 1\n" + content1 +
                           "\n## Header 2\n" + content2 +
                           "\n### Header 3\n" + content3 +
                           "\n## Header 4\n" + content4
            };
            DocumentChunker chunker = new MarkdownChunker(2);

            List<Chunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(3, chunks.Count);

            Chunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            Chunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2 + "\n### Header 3\n" + content3, chunk2);

            Chunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 4", chunk3);
            ChunkAssertions.ContentEquals(content4, chunk3);
        }
    }
}
