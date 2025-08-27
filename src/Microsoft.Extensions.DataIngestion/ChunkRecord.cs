// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;

namespace Microsoft.Extensions.DataIngestion;

public sealed class ChunkRecord<TKey> where TKey : notnull
{
    // The property names and types must match the VectorStoreCollectionDefinition created in ChunkRecordVectorStoreWriter.GetVectorStoreRecordDefinition.
    // They are annotated with attributes in case the user uses this type directly to fetch them
    // (without providing VectorStoreCollectionDefinition when creating the collection).

    // The storage names are hardcoded and lowercase with no special characters to ensure compatibility with various vector stores.
    internal const string KeyStorageName = "key";
    internal const string EmbeddingStorageName = "embedding";
    internal const string ContentStorageName = "content";
    internal const string ContextStorageName = "context";
    internal const string DocumentIdStorageName = "documentid";

    [VectorStoreKey(StorageName = KeyStorageName)]
    public TKey Key { get; set; } = default!;

    [VectorStoreVector(Dimensions: int.MaxValue, StorageName = EmbeddingStorageName)]
    public string Embedding { get; set; } = string.Empty;

    [VectorStoreData(StorageName = ContentStorageName)]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = ContextStorageName)]
    public string? Context { get; set; }

    [VectorStoreData(StorageName = DocumentIdStorageName)]
    public string DocumentId { get; set; } = string.Empty;
}
