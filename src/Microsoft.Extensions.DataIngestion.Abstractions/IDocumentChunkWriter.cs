// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public interface IDocumentChunkWriter : IDisposable
{
    Task WriteAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
}
