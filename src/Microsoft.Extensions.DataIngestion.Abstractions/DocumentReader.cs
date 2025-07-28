// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion
{
    public abstract class DocumentReader
    {
        public abstract Task<Document> ReadAsync(Stream stream, CancellationToken cancellationToken = default);

        public virtual async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // There is no need to buffer the file stream when reading a document.
            // Specifying 1 as buffer size will disable buffering on every target framework (Core and Full).
            const int DisableBuffering = 1;
            // By default, FileStream is opened for synchronous I/O operations, async is on demand.
            const bool useAsync = true;

            using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DisableBuffering, useAsync);
            return await ReadAsync(fileStream, cancellationToken);
        }

        public virtual async Task<Document> ReadAsync(Uri source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.IsFile)
            {
                return await ReadAsync(source.LocalPath, cancellationToken);
            }

            HttpClient httpClient = new();
            using HttpResponseMessage response = await httpClient.GetAsync(source, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            return await ReadAsync(stream, cancellationToken);
        }
    }
}
