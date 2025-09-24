// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public abstract class DocumentTokenChunkerTests : DocumentChunkerTests
    {

        [Fact]
        public async Task SingleChunkText()
        {
            string text = "This is a short document that fits within a single chunk.";
            Document doc = new Document("singleChunkDoc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph(text)
                }
            });

            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            DocumentChunk chunk = chunks.First();
            ChunkAssertions.ContentEquals(text, chunk);
        }
    }
}
