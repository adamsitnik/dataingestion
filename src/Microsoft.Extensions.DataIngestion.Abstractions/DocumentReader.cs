// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

// Design notes: this class no longer exposes an overload that takes a Stream and a CancellationToken.
// The reason is that Stream does not provide the necessary information like the MIME type or the file name.
public abstract class DocumentReader
{
    public Task<IngestionDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IngestionDocument>(cancellationToken);
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        string identifier = filePath; // entire path is more uniqe than just part of it.
        return ReadAsync(filePath, identifier, cancellationToken);
    }

    public abstract Task<IngestionDocument> ReadAsync(string filePath, string identifier, CancellationToken cancellationToken = default);

    public Task<IngestionDocument> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IngestionDocument>(cancellationToken);
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        string identifier = source.ToString(); // entire source is more uniqe than just part of it.
        return ReadAsync(source, identifier, cancellationToken);
    }

    public abstract Task<IngestionDocument> ReadAsync(Uri source, string identifier, CancellationToken cancellationToken = default);
}
