// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.Extensions.DataIngestion.DiagnosticsConstants;

namespace Microsoft.Extensions.DataIngestion;

public sealed class DocumentPipeline : IDocumentPipeline
{
    private readonly ActivitySource _activitySource;
    private readonly ILogger? _logger;
    private readonly DocumentReader _reader;
    private readonly IReadOnlyList<IDocumentProcessor> _processors;
    private readonly IDocumentChunker _chunker;
    private readonly IReadOnlyList<IChunkProcessor> _chunkProcessors;
    private readonly IDocumentWriter _writer;

    public DocumentPipeline(
        DocumentReader reader,
        IReadOnlyList<IDocumentProcessor> documentProcessors,
        IDocumentChunker chunker,
        IReadOnlyList<IChunkProcessor> chunkProcessors,
        IDocumentWriter writer,
        ILoggerFactory? loggerFactory = default,
        string? sourceName = default)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _processors = documentProcessors ?? throw new ArgumentNullException(nameof(documentProcessors));
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _chunkProcessors = chunkProcessors ?? throw new ArgumentNullException(nameof(chunkProcessors));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _logger = loggerFactory?.CreateLogger<DocumentPipeline>();
        _activitySource = new ActivitySource(sourceName ?? ActivitySourceName);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _activitySource.Dispose();
    }

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

        using (Activity? rootActivity = StartActivity(ProcessDirectory.ActivityName, ActivityKind.Internal))
        {
            rootActivity?.SetTag(ProcessDirectory.DirectoryPathTagName, directory.FullName);
            rootActivity?.SetTag(ProcessDirectory.SearchPatternTagName, searchPattern);
            rootActivity?.SetTag(ProcessDirectory.SearchOptionTagName, searchOption.ToString());

            _logger?.LogInformation("Starting to process files in directory '{Directory}' with search pattern '{SearchPattern}' and search option '{SearchOption}'.", directory.FullName, searchPattern, searchOption);

            IEnumerable<string> filePaths = directory.EnumerateFiles(searchPattern, searchOption).Select(fileInfo => fileInfo.FullName);
            await ProcessAsync(filePaths, cancellationToken, rootActivity);
        }
    }

    public async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (filePaths is null)
        {
            throw new ArgumentNullException(nameof(filePaths));
        }

        await ProcessAsync(filePaths, cancellationToken, parent: default);
    }

    private async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken, Activity? parent = default)
    {
        IReadOnlyList<string> filePathList = filePaths as IReadOnlyList<string> ?? filePaths.ToList();
        if (filePathList.Count == 0)
        {
            return;
        }

        using (Activity? rootActivity = StartActivity(ProcessFiles.ActivityName, ActivityKind.Internal, parent))
        {
            rootActivity?.SetTag(ProcessFiles.FileCountTagName, filePathList.Count);
            _logger?.LogInformation("Processing {FileCount} files.", filePathList.Count);

            foreach (string filePath in filePathList)
            {
                using (Activity? processFileActivity = StartActivity(ProcessFile.ActivityName, parent: rootActivity))
                {
                    processFileActivity?.SetTag(ProcessFile.FilePathTagName, filePath);
                    Document? document = null;

                    using (Activity? readerActivity = StartActivity(ReadDocument.ActivityName, ActivityKind.Client, processFileActivity))
                    {
                        readerActivity?.SetTag(ReadDocument.ReaderTagName, GetShortName(_reader));
                        _logger?.LogInformation("Reading file '{FilePath}' using '{Reader}'.", filePath, GetShortName(_reader));

                        document = await _reader.ReadAsync(filePath, cancellationToken);

                        processFileActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                        _logger?.LogInformation("Read document '{DocumentId}'.", document.Identifier);
                    }

                    await ProcessAsync(document, processFileActivity, cancellationToken);
                }
            }
        }
    }

    public async Task ProcessAsync(IEnumerable<Uri> sources, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        IReadOnlyList<Uri> sourcesList = sources as IReadOnlyList<Uri> ?? sources.ToList();
        if (sourcesList.Count == 0)
        {
            return;
        }

        using (Activity? rootActivity = StartActivity(ProcessUris.ActivityName, ActivityKind.Internal))
        {
            rootActivity?.SetTag(ProcessUris.UriCountTagName, sourcesList.Count);
            _logger?.LogInformation("Processing {UriCount} URIs.", sourcesList.Count);

            foreach (Uri source in sourcesList)
            {
                using (Activity? processUriActivity = StartActivity(ProcessUri.ActivityName, parent: rootActivity))
                {
                    processUriActivity?.SetTag(ProcessUri.UriTagName, source);
                    Document? document = null;

                    using (Activity? readerActivity = StartActivity(ReadDocument.ActivityName, ActivityKind.Client, processUriActivity))
                    {
                        readerActivity?.SetTag(ReadDocument.ReaderTagName, GetShortName(_reader));
                        _logger?.LogInformation("Reading URI '{Uri}' using '{Reader}'.", source, GetShortName(_reader));

                        document = await _reader.ReadAsync(source, cancellationToken);

                        processUriActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                        _logger?.LogInformation("Read document '{DocumentId}''.", document.Identifier);
                    }

                    await ProcessAsync(document, processUriActivity, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessAsync(Document document, Activity? parentActivity, CancellationToken cancellationToken)
    {
        foreach (IDocumentProcessor processor in _processors)
        {
            using (Activity? processorActivity = StartActivity(ProcessDocument.ActivityName, parent: parentActivity))
            {
                processorActivity?.SetTag(ProcessDocument.ProcessorTagName, GetShortName(processor));
                _logger?.LogInformation("Processing document '{DocumentId}' with '{Processor}'.", document.Identifier, GetShortName(processor));

                document = await processor.ProcessAsync(document, cancellationToken);

                // A DocumentProcessor might change the document identifier (for example by extracting it from its content), so update the ID tag.
                parentActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                _logger?.LogInformation("Processed document '{DocumentId}'.", document.Identifier);
            }
        }

        List<DocumentChunk>? chunks = null;
        using (Activity? chunkerActivity = StartActivity(ChunkDocument.ActivityName, parent: parentActivity))
        {
            chunkerActivity?.SetTag(ChunkDocument.ChunkerTagName, GetShortName(_chunker));
            _logger?.LogInformation("Chunking document '{DocumentId}' with '{Chunker}'.", document.Identifier, GetShortName(_chunker));

            chunks = await _chunker.ProcessAsync(document, cancellationToken);

            parentActivity?.SetTag(ProcessSource.ChunkCountTagName, chunks.Count);
            _logger?.LogInformation("Chunked document into {ChunkCount} chunks.", chunks.Count);
        }

        foreach (IChunkProcessor processor in _chunkProcessors)
        {
            using (Activity? processorActivity = StartActivity(ProcessChunk.ActivityName, parent: parentActivity))
            {
                processorActivity?.SetTag(ProcessChunk.ProcessorTagName, GetShortName(processor));
                _logger?.LogInformation("Processing {ChunkCount} chunks for document '{DocumentId}' with '{Processor}'.", chunks.Count, document.Identifier, GetShortName(processor));

                chunks = await processor.ProcessAsync(chunks, cancellationToken);

                // A ChunkProcessor might change the number of chunks, so update the chunk count tag.
                parentActivity?.SetTag(ProcessSource.ChunkCountTagName, chunks.Count);
                _logger?.LogInformation("Processed chunks for document '{DocumentId}'.", document.Identifier);
            }
        }

        using (Activity? writerActivity = StartActivity(WriteDocument.ActivityName, ActivityKind.Client, parentActivity))
        {
            writerActivity?.SetTag(WriteDocument.WriterTagName, GetShortName(_reader));
            _logger?.LogInformation("Persisting chunks with '{Writer}'.", GetShortName(_writer));

            await _writer.WriteAsync(document, chunks, cancellationToken);

            _logger?.LogInformation("Persisted chunks for document '{DocumentId}'.", document.Identifier);
        }
    }

    private string GetShortName(object any)
    {
        Type type = any.GetType();

        return type.IsConstructedGenericType
            ? type.ToString()
            : type.Name;
    }

    private Activity? StartActivity(string name, ActivityKind activityKind = ActivityKind.Internal, Activity? parent = default)
    {
        if (!_activitySource.HasListeners())
        {
            return null;
        }
        else if (parent is null)
        {
            return _activitySource.StartActivity(name, activityKind);
        }

        return _activitySource.StartActivity(name, activityKind, parent.Context);
    }
}
