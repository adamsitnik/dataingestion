// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
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
        DocumentWriter writer,
        ILoggerFactory? loggerFactory = default)
    {
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Processors = processors ?? throw new ArgumentNullException(nameof(processors));
        Chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        Logger = loggerFactory?.CreateLogger<DocumentPipeline>();
    }

    public DocumentReader Reader { get; }

    public IReadOnlyList<DocumentProcessor> Processors { get; }

    public DocumentChunker Chunker { get; }

    public DocumentWriter Writer { get; }

    protected ILogger? Logger { get; }

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

        Logger?.LogInformation("Starting to process files in directory '{Directory}' with search pattern '{SearchPattern}' and search option '{SearchOption}'.", directory.FullName, searchPattern, searchOption);

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
            Logger?.LogInformation("Processing file '{FilePath}' using '{Reader}'.", filePath, GetShortName(Reader));

            Document document = await Reader.ReadAsync(filePath, cancellationToken);

            Logger?.LogInformation("Read document '{DocumentId}' from file '{FilePath}'.", document.Identifier, filePath);
            Logger?.LogDebug("Document content: {Content}", document.Markdown);

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
            Logger?.LogInformation("Processing link '{Link}' using '{Reader}'.", source, GetShortName(Reader));

            Document document = await Reader.ReadAsync(source, cancellationToken);

            Logger?.LogInformation("Read document '{DocumentId}' from link '{Link}'.", document.Identifier, source);
            Logger?.LogDebug("Document content: {Content}", document.Markdown);

            await ProcessAsync(document, cancellationToken);
        }
    }

    private async Task ProcessAsync(Document document, CancellationToken cancellationToken)
    {
        foreach (DocumentProcessor processor in Processors)
        {
            Logger?.LogInformation("Processing document '{DocumentId}' with '{Processor}'.", document.Identifier, GetShortName(processor));
            document = await processor.ProcessAsync(document, cancellationToken);
            Logger?.LogInformation("Processed document '{DocumentId}'.", document.Identifier);
        }

        Logger?.LogInformation("Chunking document '{DocumentId}' with '{Chunker}'.", document.Identifier, GetShortName(Chunker));
        List<Chunk> chunks = await Chunker.ProcessAsync(document, cancellationToken);
        Logger?.LogInformation("Chunked document into {ChunkCount} chunks.", chunks.Count);

        Logger?.LogInformation("Persisting chunks with '{Writer}'.", GetShortName(Writer));
        await Writer.WriteAsync(document, chunks, cancellationToken);
        Logger?.LogInformation("Persisted chunks for document '{DocumentId}'.", document.Identifier);
    }

    private string GetShortName(object any)
    {
        Type type = any.GetType();

        return type.IsConstructedGenericType
            ? type.ToString()
            : type.Name;
    }
}
