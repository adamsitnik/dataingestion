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
    // Values taken from Semantic Kernel's TextChunker class.
    // https://github.com/microsoft/semantic-kernel/blob/167308f53c3ea15aed5ed1140210eed56474c968/dotnet/src/SemanticKernel.Core/Text/TextChunker.cs#L55-L56
    private static readonly string?[] s_plaintextSplitOptions = ["\n", ".。．", "?!", ";", ":", ",，、", ")]}", " ", "-", null];
    private static readonly string?[] s_markdownSplitOptions = [".\u3002\uFF0E", "?!", ";", ":", ",\uFF0C\u3001", ")]}", " ", "-", "\n\r", null];

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
    // 2. Maintain context by including headers from the headers array in each chunk.
    // 3. Ensure that chunks are created from complete DocumentElements, not splitting them mid-way, especially for tables!
    // 4. If a single DocumentElement exceeds _maxTokensPerChunk, it should be split intelligently (e.g., paragraphs can be split into sentences, tables into rows).
    internal void Process(List<DocumentChunk> chunks, string context, List<DocumentElement> elements)
    {
        // Token count != character count, but StringBuilder will grow as needed.
        _currentChunk ??= new(capacity: _maxTokensPerChunk);

        int contextTokenCount = _tokenizer.CountTokens(context);
        int totalTokenCount = contextTokenCount;
        Debug.Assert(_maxTokensPerChunk > totalTokenCount, "Context token count should not exceed max tokens per chunk.");
        _currentChunk.Append(context);

        for (int elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
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

                _currentChunk.AppendLine(); // separate elements by a new line
                _currentChunk.Append(semanticContent);
            }
            else if (element is DocumentTable table)
            {
                // Split by rows!
                throw new NotImplementedException("Table splitting is not implemented yet.");
            }
            else
            {
                // Split by sentences or other delimiters.

                int startIndex = 0;
                do
                {
                    int index = _tokenizer.GetIndexByTokenCount(semanticContent.AsSpan(startIndex), _maxTokensPerChunk - totalTokenCount, out string? _, out int tokenCount);

                    _currentChunk.AppendLine(); // separate elements by a new line
#if NET
                    _currentChunk.Append(semanticContent.AsSpan(startIndex, index).Trim());
#else
                    _currentChunk.Append(semanticContent.Substring(startIndex, index).Trim());
#endif
                    totalTokenCount += tokenCount;

                    chunks.Add(new(_currentChunk.ToString(), totalTokenCount, context));

                    totalTokenCount = contextTokenCount;
                    _currentChunk.Remove(startIndex: context.Length, _currentChunk.Length - context.Length); // keep the context

                    startIndex += index;
                } while (startIndex < semanticContent.Length);
            }
        }

        if (totalTokenCount > contextTokenCount)
        {
            chunks.Add(new(_currentChunk.ToString(), totalTokenCount, context));
        }
        _currentChunk.Clear();
    }
}
