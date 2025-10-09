// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class VectorStoreWriterTests
{
    public static TheoryData<VectorStore, TestEmbeddingGenerator> VectorStoreTestData
    {
        get
        {
            TestEmbeddingGenerator first = new TestEmbeddingGenerator();
            TestEmbeddingGenerator second = new TestEmbeddingGenerator();

            return new TheoryData<VectorStore, TestEmbeddingGenerator>
            {
                { new SqliteVectorStore($"Data Source={Path.GetTempFileName()};Pooling=false",
                    new() { EmbeddingGenerator = first }), first },
                { new InMemoryVectorStore(
                    new() { EmbeddingGenerator = second }), second },
            };
        }
    }

    [Theory]
    [MemberData(nameof(VectorStoreTestData))]
    public async Task CanGenerateDynamicSchema(VectorStore vectorStore, TestEmbeddingGenerator testEmbeddingGenerator)
    {
        string documentId = Guid.NewGuid().ToString();

        using VectorStoreWriter writer = new(
            vectorStore,
            dimensionCount: TestEmbeddingGenerator.DimensionCount);

        IngestionDocument document = new(documentId);
        List<IngestionChunk> chunks = new()
        {
            new IngestionChunk("some content", document)
            {
                Metadata =
                {
                    { "key1", "value1" },
                    { "key2", 123 },
                    { "key3", true },
                    { "key4", 123.45 },
                }
            }
        };

        Assert.False(testEmbeddingGenerator.WasCalled);
        await writer.WriteAsync(ToAsyncEnumerable(chunks));

        Dictionary<string, object?> record = await writer.VectorStoreCollection
            .GetAsync(filter: record => (string)record["documentid"]! == documentId, top: 1)
            .SingleAsync();

        Assert.NotNull(record);
        Assert.NotNull(record["key"]);
        Assert.Equal(documentId, record["documentid"]);
        Assert.Equal(chunks[0].Content, record["content"]);
        Assert.True(testEmbeddingGenerator.WasCalled);
        foreach (var kvp in chunks[0].Metadata)
        {
            Assert.True(record.ContainsKey(kvp.Key), $"Record does not contain key '{kvp.Key}'");
            Assert.Equal(kvp.Value, record[kvp.Key]);
        }
    }

    [Theory]
    [MemberData(nameof(VectorStoreTestData))]
    public async Task DoesSupportIncrementalIngestion(VectorStore vectorStore, TestEmbeddingGenerator _)
    {
        string documentId = Guid.NewGuid().ToString();

        using VectorStoreWriter writer = new(
            vectorStore,
            dimensionCount: TestEmbeddingGenerator.DimensionCount,
            options: new()
            {
                IncrementalIngestion = true,
            });

        IngestionDocument document = new(documentId);
        List<IngestionChunk> chunks = new()
        {
            new IngestionChunk("first chunk", document)
            {
                Metadata =
                {
                    { "key1", "value1" }
                }
            },
            new IngestionChunk("second chunk", document)
        };

        await writer.WriteAsync(ToAsyncEnumerable(chunks));

        int recordCount = await writer.VectorStoreCollection
            .GetAsync(filter: record => (string)record["documentid"]! == documentId, top: 100)
            .CountAsync();
        Assert.Equal(chunks.Count, recordCount);

        // Now we will do an incremental ingestion that updates the chunk(s).
        List<IngestionChunk> updatedChunks = new()
        {
            new IngestionChunk("different content", document)
            {
                Metadata =
                {
                    { "key1", "value2" },
                }
            }
        };

        await writer.WriteAsync(ToAsyncEnumerable(updatedChunks));

        // We ask for 100 records, but we expect only 1 as the previous 2 should have been deleted.
        Dictionary<string, object?> record = await writer.VectorStoreCollection
            .GetAsync(filter: record => (string)record["documentid"]! == documentId, top: 100)
            .SingleAsync();

        Assert.NotNull(record);
        Assert.NotNull(record["key"]);
        Assert.Equal("different content", record["content"]);
        Assert.Equal("value2", record["key1"]);
    }

    private async IAsyncEnumerable<IngestionChunk> ToAsyncEnumerable(IEnumerable<IngestionChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }
    }
}
