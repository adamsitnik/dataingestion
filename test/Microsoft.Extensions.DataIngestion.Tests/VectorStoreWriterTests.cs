// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class VectorStoreWriterTests
{
    [Fact]
    public async Task CanWriteToVectorStore()
    {
        List<Guid> ids = [];
        TestEmbeddingGenerator embeddingGenerator = new();
        InMemoryVectorStoreOptions options = new()
        {
            EmbeddingGenerator = embeddingGenerator
        };
        using InMemoryVectorStore testVectorStore = new(options);
        using InMemoryCollection<Guid, TestRecord> inMemoryCollection = testVectorStore.GetCollection<Guid, TestRecord>("testCollection");
        using VectorStoreWriter<Guid, TestRecord> vectorStoreWriter = new(inMemoryCollection, (doc, chunk) =>
        {
            Guid recordId = Guid.NewGuid();
            ids.Add(recordId);

            return new()
            {
                Id = recordId,
                Content = chunk.Content,
                DocumentId = doc.Identifier
            };
        });

        Document document = new("testDocument");
        List<Chunk> chunks = new()
        {
            new Chunk("This is the content of chunk 1.", tokenCount: int.MaxValue),
            new Chunk("This is the content of chunk 2.", tokenCount: int.MaxValue)
        };

        await vectorStoreWriter.WriteAsync(document, chunks);

        Assert.Equal(2, ids.Count);
        Assert.True(embeddingGenerator.WasCalled, "Embedding generator should have been called.");

        TestRecord[] retrieved = await inMemoryCollection.GetAsync(ids).ToArrayAsync();
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(ids[i], retrieved[i].Id);
            Assert.Equal(chunks[i].Content, retrieved[i].Content);
            Assert.Equal(document.Identifier, retrieved[i].DocumentId);
        }
    }

    public class TestRecord
    {
        [VectorStoreKey(StorageName = "key")]
        public Guid Id { get; set; }

        [VectorStoreVector(Dimensions: 4, StorageName = "embedding")]
        public string Content { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "doc_id")]
        public string DocumentId { get; set; } = string.Empty;
    }

    private class TestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public bool WasCalled { get; private set; } = false;

        public void Dispose() { }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new(new float[] { 0, 1, 2, 3 })]));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
