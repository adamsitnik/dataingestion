// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

// Design: Should the name suggest that it processes a single document or multiple documents? (DocumentsProcessor vs DocumentProcessor)
public abstract class DocumentProcessor
{
    // Design notes: plenty of processors will be sync, hence the usage of ValueTask.
    public abstract ValueTask<List<Document>> ProcessAsync(List<Document> documents, CancellationToken cancellationToken = default);
}
