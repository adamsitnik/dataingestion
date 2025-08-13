// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Extensions.DataIngestion
{
    [DebuggerDisplay("{Content}")]
    public sealed class Chunk
    {
        public string Content { get; }
        /// <summary>
        /// Chunk location relative to the source document.
        /// </summary>
        public ChunkLocation Location { get; }
        public int TokenCount { get; }

        public string? Context { get; }

        public Chunk(string content, ChunkLocation location, int tokenCount, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            if (tokenCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must be greater than zero.");

            Content = content;
            Location = location;
            TokenCount = tokenCount;
            Context = context;
        }
    }

    public struct ChunkLocation
    {
        public int Start { get; }
        public int Length { get; }

        public ChunkLocation(int start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "Start index cannot be negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");
            Start = start;
            Length = length;
        }
    }
}
