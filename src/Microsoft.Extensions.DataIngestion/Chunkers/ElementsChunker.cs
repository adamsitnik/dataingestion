// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Extensions.DataIngestion.Chunkers;

internal sealed class ElementsChunker
{
    private readonly Tokenizer _tokenizer;
    private readonly int _maxTokensPerChunk;
    private readonly int _overlapTokens;
    private StringBuilder? _currentChunk;

    internal ElementsChunker(Tokenizer tokenizer, int maxTokensPerChunk = 2_000, int overlapTokens = 500)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _maxTokensPerChunk = maxTokensPerChunk > 0 ? maxTokensPerChunk : throw new ArgumentOutOfRangeException(nameof(maxTokensPerChunk));
        _overlapTokens = overlapTokens >= 0 ? overlapTokens : throw new ArgumentOutOfRangeException(nameof(overlapTokens));
    }

    // Goals:
    // 1. Create chunks that do not exceed _maxTokensPerChunk when tokenized.
    // 2. Maintain context in each chunk.
    // 3. If a single DocumentElement exceeds _maxTokensPerChunk, it should be split intelligently (e.g., paragraphs can be split into sentences, tables into rows).
    internal void Process(List<DocumentChunk> chunks, string context, List<DocumentElement> elements)
    {
        // Token count != character count, but StringBuilder will grow as needed.
        _currentChunk ??= new(capacity: _maxTokensPerChunk);

        int contextTokenCount = _tokenizer.CountTokens(context);
        int totalTokenCount = contextTokenCount;
        // If the context itself exceeds the max tokens per chunk, we can't do anything.
        if (contextTokenCount >= _maxTokensPerChunk)
        {
            ThrowTokenCountExceeded();
        }
        _currentChunk.Append(context);

        for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
            _currentChunk.AppendLine(); // separate elements by a new line

            DocumentElement element = elements[elementIndex];
            string semanticContent = element switch
            {
                // Image exposes:
                // - Markdown: ![Alt Text](url) which is not very useful for embedding.
                // - AlternativeText: usually a short description of the image, can be null or empty. It is usually less than 50 words.
                // - Text: result of OCR, can be longer, but also can be null or empty. It can be several hundred words.
                // We prefer  AlternativeText over Text, as it is usually more relevant.
                DocumentImage image => image.AlternativeText ?? image.Text,
                _ => element.Markdown
            };

            Debug.Assert(!string.IsNullOrEmpty(semanticContent), "Element semantic content should not be null or empty.");

            int elementTokenCount = _tokenizer.CountTokens(semanticContent);
            if (elementTokenCount + totalTokenCount <= _maxTokensPerChunk)
            {
                totalTokenCount += elementTokenCount;
                _currentChunk.Append(semanticContent);
            }
            else if (element is DocumentTable table)
            {
                // Split by rows!
                throw new NotImplementedException("Table splitting is not implemented yet.");
            }
            else
            {
                ReadOnlySpan<char> remainingContent = semanticContent.AsSpan();

                while (!remainingContent.IsEmpty)
                {
                    int index = _tokenizer.GetIndexByTokenCount(
                        text: remainingContent,
                        maxTokenCount: _maxTokensPerChunk - totalTokenCount,
                        out string? normalizedText,
                        out int tokenCount,
                        considerNormalization: false); // We don't normalize, just append as-is to keep original content.

                    if (index > 0) // some tokens fit
                    {
                        // We could try to split by sentences or other delimiters, but it's complicated.
                        // For simplicity, we will just split at the last new line that fits.
                        // Our promise is not to go over the max token count, not to create perfect chunks.
                        int newLineIndex = remainingContent.Slice(0, index).LastIndexOf('\n');
                        if (newLineIndex > 0)
                        {
                            index = newLineIndex + 1; // We want to include the new line character (works for "\r\n" as well).
                            tokenCount = _tokenizer.CountTokens(remainingContent.Slice(0, index), considerNormalization: false);
                        }

                        totalTokenCount += tokenCount;
                        ReadOnlySpan<char> spanToAppend = remainingContent.Slice(0, index);
                        _currentChunk.Append(spanToAppend
#if NETSTANDARD2_0
                            .ToString()
#endif
                        );
                        remainingContent = remainingContent.Slice(index);
                    }
                    else if (totalTokenCount == contextTokenCount)
                    {
                        // We are at the beginning of a chunk, and even a single token does not fit.
                        ThrowTokenCountExceeded();
                    }

                    if (!remainingContent.IsEmpty)
                    {
                        chunks.Add(new(_currentChunk.ToString(), totalTokenCount, context));

                        // We keep the context in the current chunk as it's the same for all elements.
                        _currentChunk.Remove(
                            startIndex: context.Length + Environment.NewLine.Length,
                            length: _currentChunk.Length - (context.Length + Environment.NewLine.Length));
                        totalTokenCount = contextTokenCount;
                    }
                }
            }
        }

        if (totalTokenCount > contextTokenCount)
        {
            chunks.Add(new(_currentChunk.ToString(), totalTokenCount, context));
        }
        _currentChunk.Clear();

        static void ThrowTokenCountExceeded()
            => throw new InvalidOperationException("Can't fit in the current chunk. Consider increasing max tokens per chunk.");
    }
}
