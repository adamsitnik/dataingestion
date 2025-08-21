// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

public class DocumentPipeline
{
    public DocumentPipeline(
        DocumentReader reader,
        IReadOnlyList<DocumentProcessor> processors,
        DocumentChunker chunker,
        DocumentWriter writer)
    {
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Processors = processors ?? throw new ArgumentNullException(nameof(processors));
        Chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public DocumentReader Reader { get; }

    public IReadOnlyList<DocumentProcessor> Processors { get; }

    public DocumentChunker Chunker { get; }

    public DocumentWriter Writer { get; }

    public async Task ProcessAsync(DirectoryInfo directory, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
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
        
        IEnumerable<string> filePaths = directory.EnumerateFiles(searchPattern, searchOption).Select(fileInfo => fileInfo.FullName);
        await ProcessAsync(filePaths, cancellationToken);
    }

    public virtual async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (filePaths is null)
        {
            throw new ArgumentNullException(nameof(filePaths));
        }

        foreach (string filePath in filePaths)
        {
            Document document = await Reader.ReadAsync(filePath, cancellationToken);

            await ProcessAsync(document, cancellationToken);
        }
    }

    public virtual async Task ProcessAsync(IEnumerable<Uri> sources, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        foreach (Uri source in sources)
        {
            Document document = await Reader.ReadAsync(source, cancellationToken);

            await ProcessAsync(document, cancellationToken);
        }
    }

    private async Task ProcessAsync(Document document, CancellationToken cancellationToken)
    {
        foreach (DocumentProcessor processor in Processors)
        {
            document = await processor.ProcessAsync(document, cancellationToken);
        }

        List<Chunk> chunks = await Chunker.ProcessAsync(document, cancellationToken);

        await Writer.WriteAsync(document, chunks, cancellationToken);
    }
}
