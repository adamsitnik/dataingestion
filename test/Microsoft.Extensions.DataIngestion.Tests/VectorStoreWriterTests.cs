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
        const string CollectionName = "chunks";
        string documentId = Guid.NewGuid().ToString();
        string dbFilePath = Path.GetTempFileName();

        using VectorStoreWriter writer = new(
            vectorStore,
            dimensionCount: TestEmbeddingGenerator.DimensionCount,
            collectionName: CollectionName);

        Document document = new(documentId);
        List<DocumentChunk> chunks = new()
        {
            new DocumentChunk("some content")
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
        await writer.WriteAsync(document, chunks);

        Dictionary<string, object?>? record = await writer.VectorStoreCollection
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
}
