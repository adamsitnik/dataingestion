// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests
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
            Document noHeaerDoc = new Document("noHeaderDoc")
            {
                Markdown = "This is a document without headers.".ReplaceLineEndings()
            };
            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(noHeaerDoc);
            Assert.True(chunks.Count == 1);

            Chunk chunk = chunks.First();
            Assert.Equal("This is a document without headers.", chunk.Content.Trim());
            Assert.Equal(chunk.Context, string.Empty);
        }

        [Fact]
        public async Task SingleHeaderDocument()
        {
            Document singleHeaderDoc = new Document("singleHeaderDoc")
            {
                Markdown = "# Header 1\nThis is the content under header 1.".ReplaceLineEndings()
            };
            DocumentChunker chunker = CreateDocumentChunker();
            List<Chunk> chunks = await chunker.ProcessAsync(singleHeaderDoc);
            Assert.True(chunks.Count == 1);
            Chunk chunk = chunks.First();
            Assert.Equal("# Header 1", chunk.Context);
            Assert.Equal("This is the content under header 1.", chunk.Content.Trim());
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
            Assert.Equal("# Header 1", chunk.Context);
            Assert.Equal(content, chunk.Content.Trim());
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
            Assert.Equal("# Header 1", chunk1.Context);
            Assert.Equal(content1, chunk1.Content.Trim());
            Chunk chunk2 = chunks[1];
            Assert.Equal("# Header 1;## Header 2", chunk2.Context);
            Assert.Equal(content2, chunk2.Content.Trim());
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
            Assert.Equal("# Header 1", chunk1.Context);
            Assert.Equal(content1, chunk1.Content.Trim());
            Chunk chunk2 = chunks[1];
            Assert.Equal("# Header 2", chunk2.Context);
            Assert.Equal(content2, chunk2.Content.Trim());
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
            Assert.True(chunks.Count == 4);
            Chunk chunk1 = chunks[0];
            Assert.Equal("# Header 1", chunk1.Context);
            Assert.Equal(content1, chunk1.Content.Trim());
            Chunk chunk2 = chunks[1];
            Assert.Equal("# Header 1;## Header 2", chunk2.Context);
            Assert.Equal(content2, chunk2.Content.Trim());
            Chunk chunk3 = chunks[2];
            Assert.Equal("# Header 1;## Header 2;### Header 3", chunk3.Context);
            Assert.Equal(content3, chunk3.Content.Trim());
            Chunk chunk4 = chunks[3];
            Assert.Equal("# Header 1;## Header 4", chunk4.Context);
            Assert.Equal(content4, chunk4.Content.Trim());
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
            DocumentChunker chunker = new MarkdownChunker(MarkdownHeaderLevel.Header2);
            List<Chunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.True(chunks.Count == 3);
            Chunk chunk1 = chunks[0];
            Assert.Equal("# Header 1", chunk1.Context);
            Assert.Equal(content1, chunk1.Content.Trim());
            Chunk chunk2 = chunks[1];
            Assert.Equal("# Header 1;## Header 2", chunk2.Context);
            Assert.Equal((content2 + "\n### Header 3\n" + content3).ReplaceLineEndings(), chunk2.Content.Trim());
            Chunk chunk3 = chunks[2];
            Assert.Equal("# Header 1;## Header 4", chunk3.Context);
            Assert.Equal(content4, chunk3.Content.Trim());
        }
    }
}
