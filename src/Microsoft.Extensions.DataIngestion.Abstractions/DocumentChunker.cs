// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public abstract class DocumentChunker
{
    public abstract ValueTask<List<Chunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default);
}
