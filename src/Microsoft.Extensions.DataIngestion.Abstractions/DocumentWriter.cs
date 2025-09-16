// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

// design notes: it could become an interface if using abstract class does not bring any value.
public abstract class DocumentWriter : IDisposable
{
    public abstract Task WriteAsync(Document document, List<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    public abstract void Dispose();
}
