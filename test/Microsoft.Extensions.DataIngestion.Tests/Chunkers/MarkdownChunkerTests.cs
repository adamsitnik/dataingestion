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
            List<DocumentChunk> chunks = await chunker.ProcessAsync(noHeaerDoc);
            Assert.True(chunks.Count == 1);

            DocumentChunk chunk = chunks.First();
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
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = "This is the content under header 1.".ReplaceLineEndings()
                    }
                }
            });

            DocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(singleHeaderDoc);
            Assert.Single(chunks);
            DocumentChunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            ChunkAssertions.ContentEquals("This is the content under header 1.", chunk);
        }

        [Fact]
        public async Task SingleHeaderTwoParagraphDocument()
        {
            
            Document singleHeaderTwoParagraphDoc = new Document("singleHeaderTwoParagraphDoc");
            singleHeaderTwoParagraphDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = "This is the first paragraph."
                    },
                    new DocumentParagraph
                    {
                        Markdown = "This is the second paragraph."
                    }
                }
            });
            DocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(singleHeaderTwoParagraphDoc);
            Assert.Single(chunks);
            DocumentChunk chunk = chunks.First();
            ChunkAssertions.ContextEquals("# Header 1", chunk);
            string content = "This is the first paragraph.\nThis is the second paragraph.";
            ChunkAssertions.ContentEquals(content, chunk);
        }

        [Fact]
        public async Task MultiHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            Document multiHeaderDoc = new Document("singleHeaderTwoParagraphDoc");
            multiHeaderDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = content1
                    },
                    new DocumentHeader
                    {
                        Markdown = "## Header 2",
                        Level = 2
                    },
                    new DocumentParagraph
                    {
                        Markdown = content2
                    }
                }
            });
            DocumentChunker chunker = new MarkdownChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(multiHeaderDoc);
            Assert.Equal(2, chunks.Count);

            DocumentChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            DocumentChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);
        }

        [Fact]
        public async Task TwoHeaderDocument()
        {
            string content1 = "This is the content under header 1.".ReplaceLineEndings();
            string content2 = "This is the content under header 2.".ReplaceLineEndings();
            Document twoHeaderDoc = new Document("singleHeaderTwoParagraphDoc");
            twoHeaderDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = content1
                    },
                    new DocumentHeader
                    {
                        Markdown = "# Header 2",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = content2
                    }
                }
            });
            DocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(twoHeaderDoc);
            Assert.Equal(2, chunks.Count);

            DocumentChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            DocumentChunk chunk2 = chunks[1];
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
            Document complexDoc = new Document("complexDoc");
            complexDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = content1
                    },
                    new DocumentHeader
                    {
                        Markdown = "## Header 2",
                        Level = 2
                    },
                    new DocumentParagraph
                    {
                        Markdown = content2
                    },
                    new DocumentHeader
                    {
                        Markdown = "### Header 3",
                        Level = 3
                    },
                    new DocumentParagraph
                    {
                        Markdown = content3
                    },
                    new DocumentHeader
                    {
                        Markdown = "## Header 4",
                        Level = 2
                    },
                    new DocumentParagraph
                    {
                        Markdown = content4
                    }
                }
            });
            DocumentChunker chunker = CreateDocumentChunker();

            List<DocumentChunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(4, chunks.Count);

            DocumentChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            DocumentChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2, chunk2);

            DocumentChunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2;### Header 3", chunk3);
            ChunkAssertions.ContentEquals(content3, chunk3);

            DocumentChunk chunk4 = chunks[3];
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
            Document complexDoc = new Document("complexDoc");
            complexDoc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader
                    {
                        Markdown = "# Header 1",
                        Level = 1
                    },
                    new DocumentParagraph
                    {
                        Markdown = content1
                    },
                    new DocumentHeader
                    {
                        Markdown = "## Header 2",
                        Level = 2
                    },
                    new DocumentParagraph
                    {
                        Markdown = content2
                    },
                    new DocumentHeader
                    {
                        Markdown = "### Header 3",
                        Level = 3
                    },
                    new DocumentParagraph
                    {
                        Markdown = content3
                    },
                    new DocumentHeader
                    {
                        Markdown = "## Header 4",
                        Level = 2
                    },
                    new DocumentParagraph
                    {
                        Markdown = content4
                    }
                }
            });
            DocumentChunker chunker = new MarkdownChunker(2);

            List<DocumentChunk> chunks = await chunker.ProcessAsync(complexDoc);
            Assert.Equal(3, chunks.Count);

            DocumentChunk chunk1 = chunks[0];
            ChunkAssertions.ContextEquals("# Header 1", chunk1);
            ChunkAssertions.ContentEquals(content1, chunk1);

            DocumentChunk chunk2 = chunks[1];
            ChunkAssertions.ContextEquals("# Header 1;## Header 2", chunk2);
            ChunkAssertions.ContentEquals(content2 + "\n### Header 3\n" + content3, chunk2);

            DocumentChunk chunk3 = chunks[2];
            ChunkAssertions.ContextEquals("# Header 1;## Header 4", chunk3);
            ChunkAssertions.ContentEquals(content4, chunk3);
        }
    }
}
