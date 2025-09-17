// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public sealed class VectorStoreWriter<TKey> : DocumentWriter
    where TKey : notnull
{
    // The storage names are hardcoded and lowercase with no special characters to ensure compatibility with various vector stores.
    internal const string KeyStorageName = "key";
    internal const string EmbeddingStorageName = "embedding";
    internal const string ContentStorageName = "content";
    internal const string ContextStorageName = "context";
    internal const string DocumentIdStorageName = "documentid";

    private readonly VectorStore _vectorStore;
    private readonly int _dimensionCount;
    private readonly string? _distanceFunction;
    private readonly string _collectionName;

    private VectorStoreCollection<object, Dictionary<string, object?>>? _vectorStoreCollection;
    private readonly Func<DocumentChunk, TKey> _keyProvider;

    /// <summary>
    /// Creates a new instance of <see cref="VectorStoreCollection{TKey, Dictionary{string, object?}}"/> that uses dynamic schema to store the <see cref="DocumentChunk"/> instances as <see cref="Dictionary{string, object?}"/> using provided vector store, collection name and dimension count.
    /// </summary>
    /// <param name="vectorStore">The <see cref="VectorStore"/> to use to store the <see cref="DocumentChunk"/> instances.</param>
    /// <param name="dimensionCount">The number of dimensions that the vector has. This value is required when creating collections.</param>
    /// <param name="distanceFunction">The distance function to use when creating the collection. When not provided, the default specific to given database will be used. Check <see cref="DistanceFunction"/> for available values.</param>
    /// <param name="keyProvider">The key provider. It's optional when <typeparamref name="TKey"/> is <see cref="Guid"/> or <see cref="string"/>.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="vectorStore"/> or <paramref name="collectionName"/> are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="dimensionCount"/> is less or equal zero.</exception>
    public VectorStoreWriter(VectorStore vectorStore, int dimensionCount, string? distanceFunction = null,
        Func<DocumentChunk, TKey>? keyProvider = null, string? collectionName = "chunks")
    {
        if (keyProvider is null && typeof(TKey) != typeof(Guid) && typeof(TKey) != typeof(string))
        {
            throw new ArgumentException("A key provider must be provided when TKey is not Guid or string.", nameof(keyProvider));
        }

        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _dimensionCount = dimensionCount > 0 ? dimensionCount : throw new ArgumentOutOfRangeException(nameof(dimensionCount));
        _collectionName = string.IsNullOrEmpty(collectionName) ? throw new ArgumentNullException(nameof(collectionName)) : collectionName!;
        _distanceFunction = distanceFunction;
        _keyProvider = keyProvider ?? GenerateKey;
    }

    public VectorStoreCollection<object, Dictionary<string, object?>> VectorStoreCollection
        => _vectorStoreCollection ?? throw new InvalidOperationException("The collection has not been initialized yet. Call WriteAsync first.");

    public override void Dispose()
    {
        _vectorStore.Dispose();
        _vectorStoreCollection?.Dispose();
    }

    public override async Task WriteAsync(Document document, List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }
        else if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        if (chunks.Count == 0)
        {
            return;
        }

        if (_vectorStoreCollection is null)
        {
            // We assume that every chunk has the same metadata schema so we use the first chunk as representative.
            DocumentChunk representativeChunk = chunks[0];

            _vectorStoreCollection = _vectorStore.GetDynamicCollection(_collectionName,
                GetVectorStoreRecordDefinition(_dimensionCount, _distanceFunction, representativeChunk));

            await _vectorStoreCollection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (DocumentChunk chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, object?> record = new()
            {
                [KeyStorageName] = _keyProvider(chunk),
                [ContentStorageName] = chunk.Content,
                [EmbeddingStorageName] = chunk.Content,
                [ContextStorageName] = chunk.Context,
                [DocumentIdStorageName] = document.Identifier,
            };
    
            foreach (var metadata in chunk.Metadata)
            {
                record[metadata.Key] = metadata.Value;
            }
    
            await _vectorStoreCollection.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TKey GenerateKey(DocumentChunk chunk)
        => typeof(TKey) == typeof(Guid) ? (TKey)(object)Guid.NewGuid() : (TKey)(object)Guid.NewGuid().ToString();

    private static VectorStoreCollectionDefinition GetVectorStoreRecordDefinition(int dimensionCount, string? distanceFunction, DocumentChunk representativeChunk)
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            {
                new VectorStoreKeyProperty(KeyStorageName, typeof(TKey)),
                // By using string as the type here we allow the vector store to handle the conversion from string to the actual vector type it supports.
                new VectorStoreVectorProperty(EmbeddingStorageName, typeof(string), dimensionCount)
                {
                    DistanceFunction = distanceFunction
                },
                new VectorStoreDataProperty(ContentStorageName, typeof(string)),
                new VectorStoreDataProperty(ContextStorageName, typeof(string)),
                new VectorStoreDataProperty(DocumentIdStorageName, typeof(string))
                {
                    IsIndexed = true
                }
            }
        };

        foreach (var metadata in representativeChunk.Metadata)
        {
            Type propertyType = metadata.Value.GetType();
            definition.Properties.Add(new VectorStoreDataProperty(metadata.Key, propertyType)
            {
                // We use lowercase storage names to ensure compatibility with various vector stores.
                StorageName = metadata.Key.ToLowerInvariant()
                // We could consider indexing for certain keys like classification etc. but for now we leave it as non-indexed.
                // The reason is that not every DB supports it, moreover we would need to expose the ability to configure it.
            });
        }

        return definition;
    }
}
