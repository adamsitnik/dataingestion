// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DataIngestion;

public class ChunkerOptions
{
    // Default values come comes from https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-chunk-documents#text-split-skill-example
    private int _maxTokensPerChunk = 2_000;
    private int _overlapTokens = 500;

    /// <summary>
    /// The maximum number of tokens allowed in each chunk. Default is 2000.
    /// </summary>
    public int MaxTokensPerChunk
    {
        get => _maxTokensPerChunk;
        set => _maxTokensPerChunk = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
    /// <summary>
    /// The number of overlapping tokens between consecutive chunks. Default is 0.
    /// </summary>
    public int OverlapTokens
    {
        get => _overlapTokens;
        set => _overlapTokens = value < 0
            ? throw new ArgumentOutOfRangeException(nameof(value))
            : value >= _maxTokensPerChunk
                ? throw new ArgumentOutOfRangeException(nameof(value), "Chunk overlap must be less than chunk size.")
                : value;
    }

    /// <summary>
    /// Indicate whether to consider pre-tokenization before tokenization.
    /// </summary>
    public bool ConsiderPreTokenization { get; set; } = true;

    /// <summary>
    /// Indicate whether to consider normalization before tokenization.
    /// </summary>
    public bool ConsiderNormalization { get; set; } = true;
}
