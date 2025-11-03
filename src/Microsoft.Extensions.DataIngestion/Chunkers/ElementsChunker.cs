// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Microsoft.Extensions.DataIngestion.Chunkers.ChunkingHelpers;

namespace Microsoft.Extensions.DataIngestion.Chunkers;

internal sealed class ElementsChunker
{
    private readonly Tokenizer _tokenizer;
    private readonly int _maxTokensPerChunk;
    private readonly int _overlapTokens;
    private readonly bool _considerNormalization;
    private StringBuilder? _currentChunk;

    internal ElementsChunker(IngestionChunkerOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _tokenizer = options.Tokenizer;
        _maxTokensPerChunk = options.MaxTokensPerChunk;
        _overlapTokens = options.OverlapTokens;
        _considerNormalization = options.ConsiderNormalization;
    }

    // Goals:
    // 1. Create chunks that do not exceed _maxTokensPerChunk when tokenized.
    // 2. Maintain context in each chunk.
    // 3. If a single IngestionDocumentElement exceeds _maxTokensPerChunk, it should be split intelligently (e.g., paragraphs can be split into sentences, tables into rows).
    internal IEnumerable<IngestionChunk<string>> Process(IngestionDocument document, string context, List<IngestionDocumentElement> elements)
    {
        // Not using yield return here as we use ref structs.
        List<IngestionChunk<string>> chunks = [];
        // Token count != character count, but StringBuilder will grow as needed.
        _currentChunk ??= new(capacity: _maxTokensPerChunk);

        int contextTokenCount = CountTokens(context.AsSpan());
        int totalTokenCount = contextTokenCount;
        // If the context itself exceeds the max tokens per chunk, we can't do anything.
        if (contextTokenCount >= _maxTokensPerChunk)
        {
            ThrowTokenCountExceeded();
        }
        _currentChunk.Append(context);

        for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
            IngestionDocumentElement element = elements[elementIndex];
            string? semanticContent = element switch
            {
                // Image exposes:
                // - Markdown: ![Alt Text](url) which is not very useful for embedding.
                // - AlternativeText: usually a short description of the image, can be null or empty. It is usually less than 50 words.
                // - Text: result of OCR, can be longer, but also can be null or empty. It can be several hundred words.
                // We prefer  AlternativeText over Text, as it is usually more relevant.
                IngestionDocumentImage image => image.AlternativeText ?? image.Text,
                _ => element.GetMarkdown()
            };

            if(string.IsNullOrEmpty(semanticContent))
            {
                continue; // An image can come with Markdown, but no AlternativeText or Text.
            }

            int elementTokenCount = CountTokens(semanticContent.AsSpan());
            if (elementTokenCount + totalTokenCount <= _maxTokensPerChunk)
            {
                totalTokenCount += elementTokenCount;
                AppendNewLineAndSpan(_currentChunk, semanticContent.AsSpan());
            }
            else if (element is IngestionDocumentTable table)
            {
                ValueStringBuilder tableBuilder = new(initialCapacity: 8000);
                AddMarkdownTableRow(table, rowIndex: 0, ref tableBuilder);
                AddMarkdownTableSeparatorRow(columnCount: table.Cells.GetLength(1), ref tableBuilder);

                int headerLength = tableBuilder.Length;
                int headerTokenCount = CountTokens(tableBuilder.AsSpan());

                // We can't respect the limit if context and header themselves use more tokens.
                if (contextTokenCount + headerTokenCount >= _maxTokensPerChunk)
                {
                    tableBuilder.Dispose();
                    ThrowTokenCountExceeded();
                }

                if (headerTokenCount + totalTokenCount >= _maxTokensPerChunk)
                {
                    // We can't add the header row, so commit what we have accumulated so far.
                    Commit();
                }

                totalTokenCount += headerTokenCount;
                int tableLength = headerLength;

                int rowCount = table.Cells.GetLength(0);
                for (int rowIndex = 1; rowIndex < rowCount; rowIndex++)
                {
                    AddMarkdownTableRow(table, rowIndex, ref tableBuilder);

                    int lastRowTokens = CountTokens(tableBuilder.AsSpan(tableLength));

                    // Appending this row would exceed the limit.
                    if (totalTokenCount + lastRowTokens > _maxTokensPerChunk)
                    {
                        // We append the table as long as it's not just the header.
                        if (rowIndex != 1)
                        {
                            AppendNewLineAndSpan(_currentChunk, tableBuilder.AsSpan(0, tableLength - Environment.NewLine.Length));
                        }

                        // And commit the table we built so far.
                        Commit();
                        // Erase previous rows and keep only the header.
                        tableBuilder.Length = headerLength;
                        tableLength = headerLength;
                        totalTokenCount += headerTokenCount;

                        if (totalTokenCount + lastRowTokens > _maxTokensPerChunk)
                        {
                            // This row is simply too big even for a fresh chunk:
                            tableBuilder.Dispose();
                            ThrowTokenCountExceeded();
                        }

                        AddMarkdownTableRow(table, rowIndex, ref tableBuilder);
                    }

                    tableLength = tableBuilder.Length;
                    totalTokenCount += lastRowTokens;
                }

                AppendNewLineAndSpan(_currentChunk, tableBuilder.AsSpan(0, tableLength - Environment.NewLine.Length));
                tableBuilder.Dispose();
            }
            else
            {
                ReadOnlySpan<char> remainingContent = semanticContent.AsSpan();

                TextSplittingStrategy splittingStrategy = new DelimiterSplittingStrategy(_tokenizer, delimiter: '\n');
                List<int> splitIndices = splittingStrategy.GetSplitIndices(
                    text: remainingContent,
                    maxTokenCount: _maxTokensPerChunk - contextTokenCount).ToList();
                int chunkCount = splitIndices.Count + 1;
                splitIndices.Insert(0, 0); // to handle the first chunk
                splitIndices.Add(remainingContent.Length); // to handle the last chunk

                for (int i = 0; i < chunkCount; i++)
                {
                    ReadOnlySpan<char> spanToAppend = remainingContent.Slice(splitIndices[i], splitIndices[i+1] - splitIndices[i]);
                    int spanTokenCount = CountTokens(spanToAppend);
                    if (totalTokenCount + spanTokenCount > _maxTokensPerChunk)
                    {
                        Commit();
                    }
                    totalTokenCount += spanTokenCount;
                    AppendNewLineAndSpan(_currentChunk, spanToAppend);
                }
                if(remainingContent.Length != splitIndices[^1])
                {
                    Commit();
                }
            }

            if (totalTokenCount == _maxTokensPerChunk)
            {
                Commit();
            }
        }

        if (totalTokenCount > contextTokenCount)
        {
            chunks.Add(new(_currentChunk.ToString(), document, context));
        }
        _currentChunk.Clear();

        return chunks;

        void Commit()
        {
            chunks.Add(new(_currentChunk.ToString(), document, context));

            // We keep the context in the current chunk as it's the same for all elements.
            _currentChunk.Remove(
                startIndex: context.Length,
                length: _currentChunk.Length - context.Length);
            totalTokenCount = contextTokenCount;
        }

        
    }
    private int CountTokens(ReadOnlySpan<char> input)
        => _tokenizer.CountTokens(input, considerNormalization: _considerNormalization);

    private static void AppendNewLineAndSpan(StringBuilder stringBuilder, ReadOnlySpan<char> chars)
    {
        // Don't start an empty chunk (no context provided) with a new line.
        if (stringBuilder.Length > 0)
        {
            stringBuilder.AppendLine();
        }

        stringBuilder.Append(chars
#if NETSTANDARD2_0
                                  .ToString()
#endif
        );
    }

    private static void AddMarkdownTableRow(IngestionDocumentTable table, int rowIndex, ref ValueStringBuilder vsb)
    {
        for (int columnIndex = 0; columnIndex < table.Cells.GetLength(1); columnIndex++)
        {
            vsb.Append('|');
            vsb.Append(' ');
            string? cellContent = table.Cells[rowIndex, columnIndex] switch
            {
                null => null,
                IngestionDocumentImage img => img.AlternativeText ?? img.Text,
                IngestionDocumentElement other => other.GetMarkdown()
            };
            vsb.Append(cellContent);
            vsb.Append(' ');
        }
        vsb.Append('|');
        vsb.Append(Environment.NewLine);
    }

    private static void AddMarkdownTableSeparatorRow(int columnCount, ref ValueStringBuilder vsb)
    {
        for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            vsb.Append('|');
            vsb.Append(' ');
            vsb.Append('-', 3);
            vsb.Append(' ');
        }
        vsb.Append('|');
        vsb.Append(Environment.NewLine);
    }
}
