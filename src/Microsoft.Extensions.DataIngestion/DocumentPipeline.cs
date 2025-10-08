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
    private readonly IDocumentChunkWriter _writer;

    public DocumentPipeline(
        DocumentReader reader,
        IReadOnlyList<IDocumentProcessor> documentProcessors,
        IDocumentChunker chunker,
        IReadOnlyList<IChunkProcessor> chunkProcessors,
        IDocumentChunkWriter writer,
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

            try
            {
                await ProcessAsync(filePaths, cancellationToken, rootActivity);
            }
            catch (Exception ex)
            {
                TraceException(rootActivity, ex);

                _logger?.LogError(ex, "An error occurred while processing files in directory '{Directory}'.", directory.FullName);

                throw;
            }
        }
    }

    public async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (filePaths is null)
        {
            throw new ArgumentNullException(nameof(filePaths));
        }

        using (Activity? rootActivity = StartActivity(ProcessFiles.ActivityName, ActivityKind.Internal))
        {
            try
            {
                await ProcessAsync(filePaths, cancellationToken, rootActivity);
            }
            catch (Exception ex)
            {
                TraceException(rootActivity, ex);

                _logger?.LogError(ex, "An error occurred while processing files.");

                throw;
            }
        }
    }

    private async Task ProcessAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken, Activity? rootActivity = default)
    {
        IReadOnlyList<string> filePathList = filePaths as IReadOnlyList<string> ?? filePaths.ToList();
        if (filePathList.Count == 0)
        {
            return;
        }

        rootActivity?.SetTag(ProcessFiles.FileCountTagName, filePathList.Count);
        _logger?.LogInformation("Processing {FileCount} files.", filePathList.Count);

        foreach (string filePath in filePathList)
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

            try
            {
                await ProcessAsync(sourcesList, rootActivity, cancellationToken);
            }
            catch (Exception ex)
            {
                TraceException(rootActivity, ex);

                _logger?.LogError(ex, "An error occurred while processing URIs.");

                throw;
            }
        }

        async Task ProcessAsync(IReadOnlyList<Uri> sourcesList, Activity? rootActivity, CancellationToken cancellationToken)
        {
            foreach (Uri source in sourcesList)
            {
                using (Activity? processUriActivity = StartActivity(ProcessUri.ActivityName, parent: rootActivity))
                {
                    processUriActivity?.SetTag(ProcessUri.UriTagName, source);
                    IngestionDocument? document = null;

                    using (Activity? readerActivity = StartActivity(ReadDocument.ActivityName, ActivityKind.Client, processUriActivity))
                    {
                        readerActivity?.SetTag(ReadDocument.ReaderTagName, GetShortName(_reader));
                        _logger?.LogInformation("Reading URI '{Uri}' using '{Reader}'.", source, GetShortName(_reader));

                        document = await TryAsync(() => _reader.ReadAsync(source, cancellationToken), readerActivity, processUriActivity);

                        processUriActivity?.SetTag(ProcessSource.DocumentIdTagName, document.Identifier);
                        _logger?.LogInformation("Read document '{DocumentId}'.", document.Identifier);
                    }

                    await TryAsync(() => this.ProcessAsync(document, processUriActivity, cancellationToken), processUriActivity);
                }
            }
        }
    }

    private async Task ProcessAsync(IngestionDocument document, Activity? parentActivity, CancellationToken cancellationToken)
    {
        foreach (IDocumentProcessor processor in _processors)
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

        List<DocumentChunk>? chunks = null;
        using (Activity? chunkerActivity = StartActivity(ChunkDocument.ActivityName, parent: parentActivity))
        {
            chunkerActivity?.SetTag(ChunkDocument.ChunkerTagName, GetShortName(_chunker));
            _logger?.LogInformation("Chunking document '{DocumentId}' with '{Chunker}'.", document.Identifier, GetShortName(_chunker));

            chunks = await TryAsync(() => _chunker.ProcessAsync(document, cancellationToken), chunkerActivity);

            parentActivity?.SetTag(ProcessSource.ChunkCountTagName, chunks.Count);
            _logger?.LogInformation("Chunked document into {ChunkCount} chunks.", chunks.Count);
        }

        foreach (IChunkProcessor processor in _chunkProcessors)
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
