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
    public Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Document>(cancellationToken);
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        string identifier = System.IO.Path.GetFileName(filePath);
        return ReadAsync(filePath, identifier, cancellationToken);
    }

    public abstract Task<Document> ReadAsync(string filePath, string identifier, CancellationToken cancellationToken = default);

    public Task<Document> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Document>(cancellationToken);
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        string identifier = GetId(source);
        return ReadAsync(source, identifier, cancellationToken);
    }

    public abstract Task<Document> ReadAsync(Uri source, string identifier, CancellationToken cancellationToken = default);

    private string GetId(Uri source)
    {
        for (int i = source.Segments.Length - 1; i >= 0; i--)
        {
            // https://example.com/" would result in an "/" string in the last segment
            if (!string.IsNullOrEmpty(source.Segments[i]) && source.Segments[i] != "/")
            {
                return source.Segments[i].TrimEnd('/');
            }
        }

        return source.ToString(); // Fallback to the full URI if no segments are found
    }
}
