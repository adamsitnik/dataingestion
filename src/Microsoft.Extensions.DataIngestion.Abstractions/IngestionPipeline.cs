// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public abstract class IngestionPipeline<T> : IDisposable
{
    public IList<IngestionDocumentProcessor> DocumentProcessors { get; } = [];

    public IList<IngestionChunkProcessor<T>> ChunkProcessors { get; } = [];

    public virtual async Task ProcessAsync(DirectoryInfo directory, string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (directory is null)
        {
            throw new ArgumentNullException(nameof(directory));
        }
        else if (string.IsNullOrEmpty(searchPattern))
        {
            throw new ArgumentNullException(nameof(searchPattern));
        }
        else if (!(searchOption is SearchOption.TopDirectoryOnly or SearchOption.AllDirectories))
        {
            throw new ArgumentOutOfRangeException(nameof(searchOption));
        }

        await ProcessAsync(directory.EnumerateFiles(searchPattern, searchOption), cancellationToken);
    }

    public abstract Task ProcessAsync(IEnumerable<FileInfo> files, CancellationToken cancellationToken = default);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
