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
        protected override IDocumentChunker CreateDocumentChunker()
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new SectionChunker(tokenizer, 512, 0);
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
            string expectedResult = "This is a paragraph.\nThis is another paragraph.\n";
            Assert.Equal(expectedResult, chunks[0].Content, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public async Task TwoSections()
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
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph("This is a paragraph in section 2."),
                    new DocumentParagraph("This is another paragraph in section 2.")
                }
            });
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            string expectedResult1 = "This is a paragraph.\nThis is another paragraph.\n";
            string expectedResult2 = "This is a paragraph in section 2.\nThis is another paragraph in section 2.\n";
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
            Document doc = new Document("doc");
            var section1 = new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph("This is a paragraph in section 1."),
                    new DocumentParagraph("This is another paragraph in section 1.")
                }
            };
            var subsection1 = new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph("This is a paragraph in subsection 1.1."),
                    new DocumentParagraph("This is another paragraph in subsection 1.1.")
                }
            };
            section1.Elements.Add(subsection1);
            doc.Sections.Add(section1);
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            string expectedResult = "This is a paragraph in section 1.\nThis is another paragraph in section 1.\nThis is a paragraph in subsection 1.1.\nThis is another paragraph in subsection 1.1.\n";
            Assert.Equal(expectedResult, chunks.First().Content, ignoreLineEndingDifferences: true);
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
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            Assert.True(chunks[0].Content.Split(' ').Length <= 512);
            Assert.True(chunks[1].Content.Split(' ').Length <= 512);
            Assert.Equal(text + "\n", string.Join("", chunks.Select(c => c.Content)), ignoreLineEndingDifferences: true);
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
            Assert.Single(chunks);
            string expectedResult = "Section 1\nThis is a paragraph in section 1.\nThis is another paragraph in section 1.\n";
            Assert.Equal(expectedResult, chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal("Section 1", chunks[0].Context);
        }
    }
}
