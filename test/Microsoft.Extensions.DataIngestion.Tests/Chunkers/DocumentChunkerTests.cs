// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public abstract class DocumentChunkerTests
    {
        protected abstract IDocumentChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500);

        [Fact]
        public async Task ProcessAsync_ThrowsArgumentNullException_WhenDocumentIsNull()
        {
            var chunker = CreateDocumentChunker();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await chunker.ProcessAsync(null!));
        }

        [Fact]
        public async Task EmptyDocument()
        {
            IngestionDocument emptyDoc = new("emptyDoc");
            IDocumentChunker chunker = CreateDocumentChunker();

            List<DocumentChunk> chunks = await chunker.ProcessAsync(emptyDoc);
            Assert.Empty(chunks);
        }
    }
}
