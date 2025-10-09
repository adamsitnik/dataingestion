// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class SectionChunkerTests : DocumentChunkerTests
    {
        protected override IngestionChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new SectionChunker(tokenizer, new() { MaxTokensPerChunk = maxTokensPerChunk, OverlapTokens = overlapTokens });
        }

        [Fact]
        public async Task OneSection()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph("This is a paragraph."),
                    new IngestionDocumentParagraph("This is another paragraph.")
                }
            });
            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            string expectedResult = "This is a paragraph.\nThis is another paragraph.";
            Assert.Equal(expectedResult, chunks[0].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task TwoSections()
        {
            IngestionDocument doc = new("doc")
            {
                Sections =
                {
                    new()
                    {
                        Elements =
                        {
                            new IngestionDocumentParagraph("This is a paragraph."),
                            new IngestionDocumentParagraph("This is another paragraph.")
                        }
                    },
                    new()
                    {
                        Elements =
                        {
                            new IngestionDocumentParagraph("This is a paragraph in section 2."),
                            new IngestionDocumentParagraph("This is another paragraph in section 2.")
                        }
                    }
                }
            };

            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);

            Assert.Equal(2, chunks.Count);
            string expectedResult1 = "This is a paragraph.\nThis is another paragraph.";
            string expectedResult2 = "This is a paragraph in section 2.\nThis is another paragraph in section 2.";
            Assert.Equal(expectedResult1, chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(expectedResult2, chunks[1].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task EmptySection()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements = { }
            });
            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Empty(chunks);
        }

        [Fact]
        public async Task NestedSections()
        {
            IngestionDocument doc = new("doc")
            {
                Sections =
                {
                    new()
                    {
                        Elements =
                        {
                            new IngestionDocumentHeader("# Section title"),
                            new IngestionDocumentParagraph("This is a paragraph in section 1."),
                            new IngestionDocumentParagraph("This is another paragraph in section 1."),
                            new IngestionDocumentSection
                            {
                                Elements =
                                {
                                    new IngestionDocumentHeader("## Subsection title"),
                                    new IngestionDocumentParagraph("This is a paragraph in subsection 1.1."),
                                    new IngestionDocumentParagraph("This is another paragraph in subsection 1.1.")
                                }
                            }
                        }
                    }
                }
            };

            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);

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
            IngestionDocument doc = new IngestionDocument("twoChunksNoOverlapDoc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph(text)
                }
            });
            IngestionChunker chunker = CreateDocumentChunker(maxTokensPerChunk: 512);
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            Assert.True(chunks[0].Content.Split(' ').Length <= 512);
            Assert.True(chunks[1].Content.Split(' ').Length <= 512);
            Assert.Equal(text, string.Join("", chunks.Select(c => c.Content)), ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task SectionWithHeader()
        {
            IngestionDocument doc = new IngestionDocument("doc");
            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentHeader("Section 1"),
                    new IngestionDocumentParagraph("This is a paragraph in section 1."),
                    new IngestionDocumentParagraph("This is another paragraph in section 1.")
                }
            });
            IngestionChunker chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk> chunks = await chunker.ProcessAsync(doc);
            IngestionChunk chunk = Assert.Single(chunks);
            string expectedResult = "Section 1\nThis is a paragraph in section 1.\nThis is another paragraph in section 1.";
            Assert.Equal(expectedResult, chunk.Content, ignoreLineEndingDifferences: true);
            Assert.Equal("Section 1", chunk.Context);
        }
    }
}
