// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public sealed class VectorStoreWriter<TKey, TRecord> : DocumentWriter
    where TKey : notnull
    where TRecord : class
{
    private readonly VectorStoreCollection<TKey, TRecord> _vectorStoreCollection;
    private readonly Func<Document, DocumentChunk, TRecord> _mapper;
    private bool _existsChecked = false;

    public VectorStoreWriter(VectorStoreCollection<TKey, TRecord> vectorStoreCollection, Func<Document, DocumentChunk, TRecord> mapper)
    {
        _vectorStoreCollection = vectorStoreCollection ?? throw new ArgumentNullException(nameof(vectorStoreCollection));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public override void Dispose() => _vectorStoreCollection.Dispose();

    public override async Task WriteAsync(Document document, List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_existsChecked)
        {
            await _vectorStoreCollection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

            _existsChecked = true;
        }

        foreach (DocumentChunk chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TRecord record = _mapper(document, chunk);

            await _vectorStoreCollection.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        }
    }
}
