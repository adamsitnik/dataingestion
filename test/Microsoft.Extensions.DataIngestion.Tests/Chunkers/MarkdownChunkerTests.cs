// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class MarkdownChunkerTests : DocumentChunkerTests
    {
        override protected IngestionChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            return new MarkdownChunker();
        }

        [Fact]
        public async Task NoheaderDocument()
        {
            IngestionDocument noHeaerDoc = new IngestionDocument("noHeaderDoc");
            noHeaerDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph("This is a document without headers.")
                }
            });

            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(noHeaerDoc);
            Assert.True(chunks.Count == 1);

            IngestionChunk chunk = chunks.First();
            ChunkAssertions.ContextEquals(string.Empty, chunk);
            ChunkAssertions.ContentEquals("This is a document without headers.", chunk);
        }

        [Fact]
        public async Task SingleHeaderDocument()
        {
            IngestionDocument singleHeaderDoc = new IngestionDocument("singleHeaderDoc");
            singleHeaderDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph("This is the content under header 1.")
                }
            });

            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(singleHeaderDoc);
            Assert.Single(chunks);
            IngestionChunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            ChunkAssertions.ContentEquals("This is the content under header 1.", chunk);
        }

        [Fact]
        public async Task SingleHeaderTwoParagraphDocument()
        {
            IngestionDocument singleHeaderTwoParagraphDoc = new IngestionDocument("singleHeaderTwoParagraphDoc");
            singleHeaderTwoParagraphDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph("This is the first paragraph."),
                    new IngestionDocumentParagraph("This is the second paragraph.")
                }
            });
            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(singleHeaderTwoParagraphDoc);
            Assert.Single(chunks);
            IngestionChunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            string content = "This is the first paragraph.\nThis is the second paragraph.";
            ChunkAssertions.ContentEquals(content, chunk);
        }

        [Fact]
        public async Task MultiHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            IngestionDocument multiHeaderDoc = new IngestionDocument("singleHeaderTwoParagraphDoc");
            multiHeaderDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph(content1),
                    new IngestionDocumentHeader("## Header 2")
                    {
                        Level = 2
                    },
                    new IngestionDocumentParagraph(content2)
                }
            });
            IngestionChunker chunker = new MarkdownChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(multiHeaderDoc);
            Assert.Equal(2, chunks.Count);

            IngestionChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            IngestionChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);
        }

        [Fact]
        public async Task TwoHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            IngestionDocument twoHeaderDoc = new IngestionDocument("singleHeaderTwoParagraphDoc");
            twoHeaderDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph(content1),
                    new IngestionDocumentHeader("# Header 2")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph(content2)
                }
            });
            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(twoHeaderDoc);
            Assert.Equal(2, chunks.Count);

            IngestionChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            IngestionChunk chunk2 = chunks[1];
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
            IngestionDocument complexDoc = new IngestionDocument("complexDoc");
            complexDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph(content1),
                    new IngestionDocumentHeader("## Header 2")
                    {
                        Level = 2
                    },
                    new IngestionDocumentParagraph(content2),
                    new IngestionDocumentHeader("### Header 3")
                    {
                        Level = 3
                    },
                    new IngestionDocumentParagraph(content3),
                    new IngestionDocumentHeader("## Header 4")
                    {
                        Level = 2
                    },
                    new IngestionDocumentParagraph(content4)
                }
            });
            IngestionChunker chunker = CreateDocumentChunker();

            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(4, chunks.Count);

            IngestionChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            IngestionChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);

            IngestionChunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2;### Header 3", chunk3);
            ChunkAssertions.ContentEquals(content3, chunk3);

            IngestionChunk chunk4 = chunks[3];
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
            IngestionDocument complexDoc = new IngestionDocument("complexDoc");
            complexDoc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("# Header 1")
                    {
                        Level = 1
                    },
                    new IngestionDocumentParagraph(content1),
                    new IngestionDocumentHeader("## Header 2")
                    {
                        Level = 2
                    },
                    new IngestionDocumentParagraph(content2),
                    new IngestionDocumentHeader("### Header 3")
                    {
                        Level = 3
                    },
                    new IngestionDocumentParagraph(content3),
                    new IngestionDocumentHeader("## Header 4")
                    {
                        Level = 2
                    },
                    new IngestionDocumentParagraph(content4)
                }
            });
            IngestionChunker chunker = new MarkdownChunker(2);

            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(3, chunks.Count);

            IngestionChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            IngestionChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2 + "\n### Header 3\n" + content3, chunk2);

            IngestionChunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 4", chunk3);
            ChunkAssertions.ContentEquals(content4, chunk3);
        }
    }
}
