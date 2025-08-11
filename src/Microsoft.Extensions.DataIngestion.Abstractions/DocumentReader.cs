// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion
{
    // Design notes: this class no longer exposes an overload that takes a Stream and a CancellationToken.
    // The reason is that Stream does not provide the necessary information like the MIME type or the file name.
    public abstract class DocumentReader
    {
        public abstract Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default);

        public abstract Task<Document> ReadAsync(Uri source, CancellationToken cancellationToken = default);
    }
}
