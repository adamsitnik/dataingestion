// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public sealed class ChunkRecordWriter<TKey> : DocumentWriter
    where TKey : notnull
{
    private readonly VectorStoreCollection<TKey, ChunkRecord<TKey>> _vectorStoreCollection;
    private readonly VectorStoreWriter<TKey, ChunkRecord<TKey>> _innerWriter;
    private readonly Func<Chunk, TKey> _keyProvider;

    /// <summary>
    /// Creates a new instance of <see cref="ChunkRecordWriter{TKey}"/> that uses default schema to store the <see cref="Chunk"/> instances as <see cref="ChunkRecord{TKey}"/> using provided vector store, collection name and dimension count.
    /// </summary>
    /// <param name="vectorStore">The <see cref="VectorStore"/> to use to store the <see cref="Chunk"/> instances.</param>
    /// <param name="dimensionCount">The number of dimensions that the vector has. This value is required when creating collections.</param>
    /// <param name="distanceFunction">The distance function to use when creating the collection. When not provided, the default specific to given database will be used. Check <see cref="DistanceFunction"/> for available values.</param>
    /// <param name="keyProvider">The key provider. It's optional when <typeparamref name="TKey"/> is <see cref="Guid"/> or <see cref="string"/>.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="vectorStore"/> or <paramref name="collectionName"/> are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="dimensionCount"/> is less or equal zero.</exception>
    public ChunkRecordWriter(VectorStore vectorStore, int dimensionCount, string? distanceFunction = null,
        Func<Chunk, TKey>? keyProvider = null, string? collectionName = "chunks")
    {
        if (vectorStore is null)
        {
            throw new ArgumentNullException(nameof(vectorStore));
        }
        else if (dimensionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensionCount), "Dimension count must be greater than zero.");
        }
        else if (keyProvider is null && typeof(TKey) != typeof(Guid) && typeof(TKey) != typeof(string))
        {
            throw new ArgumentException("A key provider must be provided when TKey is not Guid or string.", nameof(keyProvider));
        }
        else if (string.IsNullOrEmpty(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        _keyProvider = keyProvider ?? GenerateKey;

        _vectorStoreCollection = vectorStore.GetCollection<TKey, ChunkRecord<TKey>>(
            collectionName!, GetVectorStoreRecordDefinition(dimensionCount, distanceFunction));
        _innerWriter = new VectorStoreWriter<TKey, ChunkRecord<TKey>>(_vectorStoreCollection, Map);
    }

    public override void Dispose() => _innerWriter.Dispose();

    public VectorStoreCollection<TKey, ChunkRecord<TKey>> VectorStoreCollection => _vectorStoreCollection;

    public override Task WriteAsync(Document document, List<Chunk> chunks, CancellationToken cancellationToken = default)
        => _innerWriter.WriteAsync(document, chunks, cancellationToken);

    private static TKey GenerateKey(Chunk chunk)
        => typeof(TKey) == typeof(Guid) ? (TKey)(object)Guid.NewGuid() : (TKey)(object)Guid.NewGuid().ToString();

    private static VectorStoreCollectionDefinition GetVectorStoreRecordDefinition(int dimensionCount, string? distanceFunction)
        => new()
        {
            Properties =
            {
                new VectorStoreKeyProperty(nameof(ChunkRecord<TKey>.Key), typeof(TKey))
                {
                    StorageName = ChunkRecord<TKey>.KeyStorageName
                },
                new VectorStoreVectorProperty(nameof(ChunkRecord<TKey>.Embedding), typeof(string), dimensionCount)
                {
                    StorageName = ChunkRecord<TKey>.EmbeddingStorageName,
                    DistanceFunction = distanceFunction
                },
                new VectorStoreDataProperty(nameof(ChunkRecord<TKey>.Content), typeof(string))
                {
                    StorageName = ChunkRecord<TKey>.ContentStorageName
                },
                new VectorStoreDataProperty(nameof(ChunkRecord<TKey>.Context), typeof(string))
                {
                    StorageName = ChunkRecord<TKey>.ContextStorageName
                },
                new VectorStoreDataProperty(nameof(ChunkRecord<TKey>.DocumentId), typeof(string))
                {
                    StorageName = ChunkRecord<TKey>.DocumentIdStorageName,
                    IsIndexed = true
                }
            }
        };

    private ChunkRecord<TKey> Map(Document document, Chunk chunk)
        => new()
        {
            Key = _keyProvider(chunk),
            Content = chunk.Content,
            Context = chunk.Context,
            DocumentId = document.Identifier
        };
}
