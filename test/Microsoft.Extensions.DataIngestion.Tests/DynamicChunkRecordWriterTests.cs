// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DynamicChunkRecordWriterTests
{
    [Fact]
    public async Task CanGenerateDynamicSchema()
    {
        const string CollectionName = "chunks";
        string key = Guid.NewGuid().ToString();
        string dbFilePath = Path.GetTempFileName();

        TestEmbeddingGenerator testEmbeddingGenerator = new();
        using SqliteVectorStore sqliteVectorStore = new(
            $"Data Source={dbFilePath};Pooling=false",
            new() { EmbeddingGenerator = testEmbeddingGenerator });
        using DynamicChunkRecordWriter<string> writer = new(
            sqliteVectorStore,
            dimensionCount: TestEmbeddingGenerator.DimensionCount,
            keyProvider: _ => key,
            collectionName: CollectionName);

        Document document = new("test");
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

        Dictionary<string, object?>? record = await writer.VectorStoreCollection.GetAsync(key);

        Assert.NotNull(record);
        Assert.Equal(key, record["Key"]);
        Assert.Equal(chunks[0].Content, record["Content"]);
        Assert.True(testEmbeddingGenerator.WasCalled);
        foreach (var kvp in chunks[0].Metadata)
        {
            Assert.True(record.ContainsKey(kvp.Key), $"Record does not contain key '{kvp.Key}'");
            Assert.Equal(kvp.Value, record[kvp.Key]);
        }
    }
}
