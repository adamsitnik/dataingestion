// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class SectionChunkerTests : DocumentChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new SectionChunker(tokenizer, maxTokensPerChunk, overlapTokens);
        }

        [Fact]
        public async Task OneSection()
        {
            Document doc = new Document("doc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph("This is a paragraph."),
                    new DocumentParagraph("This is another paragraph.")
                }
            });
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            string expectedResult = "This is a paragraph.\nThis is another paragraph.";
            Assert.Equal(expectedResult, chunks[0].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task TwoSections()
        {
            Document doc = new("doc")
            {
                Sections =
                {
                    new()
                    {
                        Elements =
                        {
                            new DocumentParagraph("This is a paragraph."),
                            new DocumentParagraph("This is another paragraph.")
                        }
                    },
                    new()
                    {
                        Elements =
                        {
                            new DocumentParagraph("This is a paragraph in section 2."),
                            new DocumentParagraph("This is another paragraph in section 2.")
                        }
                    }
                }
            };

            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

            Assert.Equal(2, chunks.Count);
            string expectedResult1 = "This is a paragraph.\nThis is another paragraph.";
            string expectedResult2 = "This is a paragraph in section 2.\nThis is another paragraph in section 2.";
            Assert.Equal(expectedResult1, chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(expectedResult2, chunks[1].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task EmptySection()
        {
            Document doc = new Document("doc");
            doc.Sections.Add(new DocumentSection
            {
                Elements = { }
            });
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task NestedSections()
        {
            Document doc = new("doc")
            {
                Sections =
                {
                    new()
                    {
                        Elements =
                        {
                            new DocumentHeader("# Section title"),
                            new DocumentParagraph("This is a paragraph in section 1."),
                            new DocumentParagraph("This is another paragraph in section 1."),
                            new DocumentSection
                            {
                                Elements =
                                {
                                    new DocumentHeader("## Subsection title"),
                                    new DocumentParagraph("This is a paragraph in subsection 1.1."),
                                    new DocumentParagraph("This is another paragraph in subsection 1.1.")
                                }
                            }
                        }
                    }
                }
            };

            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);

            Assert.Equal(2, chunks.Count);
            Assert.Equal("# Section title", chunks[0].Context);
            Assert.Equal("# Section title\nThis is a paragraph in section 1.\nThis is another paragraph in section 1.", chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal("# Section title ## Subsection title", chunks[1].Context);
            Assert.Equal("# Section title ## Subsection title\nThis is a paragraph in subsection 1.1.\nThis is another paragraph in subsection 1.1.", chunks[1].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task SizeLimit_TwoChunks()
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
            Assert.Equal(text, string.Join("", chunks.Select(c => c.Content)), ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task SectionWithHeader()
        {
            Document doc = new Document("doc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader("Section 1"),
                    new DocumentParagraph("This is a paragraph in section 1."),
                    new DocumentParagraph("This is another paragraph in section 1.")
                }
            });
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            DocumentChunk chunk = Assert.Single(chunks);
            string expectedResult = "Section 1\nThis is a paragraph in section 1.\nThis is another paragraph in section 1.";
            Assert.Equal(expectedResult, chunk.Content, ignoreLineEndingDifferences: true);
            Assert.Equal("Section 1", chunk.Context);
        }
    }
}
