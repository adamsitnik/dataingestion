// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public interface IDocumentProcessor
{
    Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default);
}
