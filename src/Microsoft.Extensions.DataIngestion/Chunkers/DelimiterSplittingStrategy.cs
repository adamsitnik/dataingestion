// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Extensions.DataIngestion.Chunkers.ChunkingHelpers;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    internal class DelimiterSplittingStrategy : TextSplittingStrategy
    {
        private readonly char _delimiter;
        private readonly Tokenizer _tokenizer;

        public DelimiterSplittingStrategy(Tokenizer tokenizer, char delimiter)
        {
            _delimiter = delimiter;
            _tokenizer = tokenizer;
        }
        public override IEnumerable<int> GetSplitIndices(ReadOnlySpan<char> text, int maxTokenCount)
        {
            List<int> indices = new();
            int currentOffset = 0;
            
            while (currentOffset < text.Length)
            {
                ReadOnlySpan<char> remainingText = text.Slice(currentOffset);
                
                int index = _tokenizer.GetIndexByTokenCount(
                    text: remainingText,
                    maxTokenCount: maxTokenCount,
                    out string? normalizedText,
                    out int tokenCount,
                    considerNormalization: false); // We don't normalize, just append as-is to keep original content.

                if (index > 0) // some tokens fit
                {
                    // We could try to split by sentences or other delimiters, but it's complicated.
                    // For simplicity, we will just split at the last new line that fits.
                    // Our promise is not to go over the max token count, not to create perfect chunks.
                    int newLineIndex = remainingText.Slice(0, index).LastIndexOf('\n');
                    if (newLineIndex > 0)
                    {
                        index = newLineIndex + 1; // We want to include the new line character (works for "\r\n" as well).
                    }

                    currentOffset += index;
                    indices.Add(currentOffset);
                }
                else
                {
                    ThrowTokenCountExceeded();
                }
            }

            return indices;
        }
    }
}
