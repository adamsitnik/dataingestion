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

public class DocumentPipeline : IDocumentPipeline
{
    private readonly ILogger? _logger;

    public DocumentPipeline(
        DocumentReader reader,
        IReadOnlyList<IDocumentProcessor> documentProcessors,
        IDocumentChunker chunker,
        IReadOnlyList<IChunkProcessor> chunkProcessors,
        IDocumentWriter writer,
        ILoggerFactory? loggerFactory = default)
    {
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Processors = documentProcessors ?? throw new ArgumentNullException(nameof(documentProcessors));
        Chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        ChunkProcessors = chunkProcessors ?? throw new ArgumentNullException(nameof(chunkProcessors));
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _logger = loggerFactory?.CreateLogger<DocumentPipeline>();
    }

    public DocumentReader Reader { get; }

    public IReadOnlyList<IDocumentProcessor> Processors { get; }

    public IDocumentChunker Chunker { get; }

    public IReadOnlyList<IChunkProcessor> ChunkProcessors { get; }

    public IDocumentWriter Writer { get; }

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

        _logger?.LogInformation("Starting to process files in directory '{Directory}' with search pattern '{SearchPattern}' and search option '{SearchOption}'.", directory.FullName, searchPattern, searchOption);

        IEnumerable<string> filePaths = directory.EnumerateFiles(searchPattern, searchOption).Select(fileInfo => fileInfo.FullName);
        await ProcessAsync(filePaths, cancellationToken);
    }

    public async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (filePaths is null)
        {
            throw new ArgumentNullException(nameof(filePaths));
        }

        foreach (string filePath in filePaths)
        {
            _logger?.LogInformation("Processing file '{FilePath}' using '{Reader}'.", filePath, GetShortName(Reader));

            Document document = await Reader.ReadAsync(filePath, cancellationToken);

            _logger?.LogInformation("Read document '{DocumentId}' from file '{FilePath}'.", document.Identifier, filePath);
            _logger?.LogDebug("Document content: {Content}", document.Markdown);

            await ProcessAsync(document, cancellationToken);
        }
    }

    public async Task ProcessAsync(IEnumerable<Uri> sources, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        foreach (Uri source in sources)
        {
            _logger?.LogInformation("Processing link '{Link}' using '{Reader}'.", source, GetShortName(Reader));

            Document document = await Reader.ReadAsync(source, cancellationToken);

            _logger?.LogInformation("Read document '{DocumentId}' from link '{Link}'.", document.Identifier, source);
            _logger?.LogDebug("Document content: {Content}", document.Markdown);

            await ProcessAsync(document, cancellationToken);
        }
    }

    private async Task ProcessAsync(Document document, CancellationToken cancellationToken)
    {
        foreach (IDocumentProcessor processor in Processors)
        {
            _logger?.LogInformation("Processing document '{DocumentId}' with '{Processor}'.", document.Identifier, GetShortName(processor));
            document = await processor.ProcessAsync(document, cancellationToken);
            _logger?.LogInformation("Processed document '{DocumentId}'.", document.Identifier);
        }

        _logger?.LogInformation("Chunking document '{DocumentId}' with '{Chunker}'.", document.Identifier, GetShortName(Chunker));
        List<DocumentChunk> chunks = await Chunker.ProcessAsync(document, cancellationToken);
        _logger?.LogInformation("Chunked document into {ChunkCount} chunks.", chunks.Count);

        foreach (IChunkProcessor processor in ChunkProcessors)
        {
            _logger?.LogInformation("Processing {ChunkCount} chunks for document '{DocumentId}' with '{Processor}'.", chunks.Count, document.Identifier, GetShortName(processor));
            chunks = await processor.ProcessAsync(chunks, cancellationToken);
            _logger?.LogInformation("Processed chunks for document '{DocumentId}'.", document.Identifier);
        }

        _logger?.LogInformation("Persisting chunks with '{Writer}'.", GetShortName(Writer));
        await Writer.WriteAsync(document, chunks, cancellationToken);
        _logger?.LogInformation("Persisted chunks for document '{DocumentId}'.", document.Identifier);
    }

    private string GetShortName(object any)
    {
        Type type = any.GetType();

        return type.IsConstructedGenericType
            ? type.ToString()
            : type.Name;
    }
}
