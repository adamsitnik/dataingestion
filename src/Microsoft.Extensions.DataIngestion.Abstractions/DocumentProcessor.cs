// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public abstract class DocumentProcessor
{
    // Design notes: plenty of processors will be sync, hence the usage of ValueTask.
    public abstract ValueTask<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default);
}
