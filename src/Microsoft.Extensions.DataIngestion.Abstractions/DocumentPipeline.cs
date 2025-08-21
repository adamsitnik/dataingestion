// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
