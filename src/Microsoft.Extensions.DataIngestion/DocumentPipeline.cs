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

public sealed class DocumentPipeline : IngestionPipeline
{
    private readonly ActivitySource _activitySource;
    private readonly ILogger? _logger;
    private readonly IngestionDocumentReader _reader;
    private readonly IReadOnlyList<IngestionDocumentProcessor> _processors;
    private readonly IngestionChunker _chunker;
    private readonly IReadOnlyList<IngestionChunkProcessor> _chunkProcessors;
    private readonly IngestionChunkWriter _writer;

    public DocumentPipeline(
        IngestionDocumentReader reader,
        IReadOnlyList<IngestionDocumentProcessor> documentProcessors,
        IngestionChunker chunker,
        IReadOnlyList<IngestionChunkProcessor> chunkProcessors,
        IngestionChunkWriter writer,
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

            IEnumerable<FileInfo> files = directory.EnumerateFiles(searchPattern, searchOption);

            try
            {
                await ProcessAsync(files, cancellationToken, rootActivity);
            }
            catch (Exception ex)
            {
                TraceException(rootActivity, ex);

                _logger?.LogError(ex, "An error occurred while processing files in directory '{Directory}'.", directory.FullName);

                throw;
            }
        }
    }

    public async Task ProcessAsync(IEnumerable<FileInfo> files, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        using (Activity? rootActivity = StartActivity(ProcessFiles.ActivityName, ActivityKind.Internal))
        {
            try
            {
                await ProcessAsync(files, cancellationToken, rootActivity);
            }
            catch (Exception ex)
            {
                TraceException(rootActivity, ex);

                _logger?.LogError(ex, "An error occurred while processing files.");

                throw;
            }
        }
    }

    private async Task ProcessAsync(IEnumerable<FileInfo> files, CancellationToken cancellationToken, Activity? rootActivity = default)
    {
        IReadOnlyList<FileInfo> filesLisit = files as IReadOnlyList<FileInfo> ?? files.ToList();
        if (filesLisit.Count == 0)
        {
            return;
        }

        rootActivity?.SetTag(ProcessFiles.FileCountTagName, filesLisit.Count);
        _logger?.LogInformation("Processing {FileCount} files.", filesLisit.Count);

        foreach (FileInfo filePath in filesLisit)
        {
            using (Activity? processFileActivity = StartActivity(ProcessFile.ActivityName, parent: rootActivity))
            {
                processFileActivity?.SetTag(ProcessFile.FilePathTagName, filePath);
                IngestionDocument? document = null;

                using (Activity? readerActivity = StartActivity(ReadDocument.ActivityName, ActivityKind.Client, processFileActivity))
                {
                    readerActivity?.SetTag(ReadDocument.ReaderTagName, GetShortName(_reader));
                    _logger?.LogInformation("Reading file '{FilePath}' using '{Reader}'.", filePath, GetShortName(_reader));

                    document = await TryAsync(() => _reader.ReadAsync(filePath, cancellationToken), readerActivity, processFileActivity);

                    processFileActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                    _logger?.LogInformation("Read document '{DocumentId}'.", document.Identifier);
                }

                await TryAsync(() => ProcessAsync(document, processFileActivity, cancellationToken), processFileActivity);
            }
        }
    }

    private async Task ProcessAsync(IngestionDocument document, Activity? parentActivity, CancellationToken cancellationToken)
    {
        foreach (IngestionDocumentProcessor processor in _processors)
        {
            using (Activity? processorActivity = StartActivity(ProcessDocument.ActivityName, parent: parentActivity))
            {
                processorActivity?.SetTag(ProcessDocument.ProcessorTagName, GetShortName(processor));
                _logger?.LogInformation("Processing document '{DocumentId}' with '{Processor}'.", document.Identifier, GetShortName(processor));

                document = await TryAsync(() => processor.ProcessAsync(document, cancellationToken), processorActivity);

                // A DocumentProcessor might change the document identifier (for example by extracting it from its content), so update the ID tag.
                parentActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                _logger?.LogInformation("Processed document '{DocumentId}'.", document.Identifier);
            }
        }

        List<IngestionChunk>? chunks = null;
        using (Activity? chunkerActivity = StartActivity(ChunkDocument.ActivityName, parent: parentActivity))
        {
            chunkerActivity?.SetTag(ChunkDocument.ChunkerTagName, GetShortName(_chunker));
            _logger?.LogInformation("Chunking document '{DocumentId}' with '{Chunker}'.", document.Identifier, GetShortName(_chunker));

            chunks = await TryAsync(() => _chunker.ProcessAsync(document, cancellationToken), chunkerActivity);

            parentActivity?.SetTag(ProcessSource.ChunkCountTagName, chunks.Count);
            _logger?.LogInformation("Chunked document into {ChunkCount} chunks.", chunks.Count);
        }

        foreach (IngestionChunkProcessor processor in _chunkProcessors)
        {
            using (Activity? processorActivity = StartActivity(ProcessChunk.ActivityName, parent: parentActivity))
            {
                processorActivity?.SetTag(ProcessChunk.ProcessorTagName, GetShortName(processor));
                _logger?.LogInformation("Processing {ChunkCount} chunks for document '{DocumentId}' with '{Processor}'.", chunks.Count, document.Identifier, GetShortName(processor));

                chunks = await TryAsync(() => processor.ProcessAsync(chunks, cancellationToken), processorActivity);

                // A ChunkProcessor might change the number of chunks, so update the chunk count tag.
                parentActivity?.SetTag(ProcessSource.ChunkCountTagName, chunks.Count);
                _logger?.LogInformation("Processed chunks for document '{DocumentId}'.", document.Identifier);
            }
        }

        using (Activity? writerActivity = StartActivity(WriteDocument.ActivityName, ActivityKind.Client, parentActivity))
        {
            writerActivity?.SetTag(WriteDocument.WriterTagName, GetShortName(_writer));
            _logger?.LogInformation("Persisting chunks with '{Writer}'.", GetShortName(_writer));

            await TryAsync(() => _writer.WriteAsync(chunks, cancellationToken), writerActivity);

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

    private static async Task<T> TryAsync<T>(Func<Task<T>> func, Activity? activity, Activity? parentActivity = default)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            TraceException(activity, ex);
            TraceException(parentActivity, ex);

            throw;
        }
    }

    private static async Task TryAsync(Func<Task> func, Activity? activity)
    {
        try
        {
            await func();
        }
        catch (Exception ex)
        {
            TraceException(activity, ex);

            throw;
        }
    }

    private static void TraceException(Activity? activity, Exception ex)
    {
        activity?.SetTag(ErrorTypeTagName, ex.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}
