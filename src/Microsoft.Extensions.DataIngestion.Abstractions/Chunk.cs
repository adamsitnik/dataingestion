// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    [DebuggerDisplay("{Content}")]
    public sealed class Chunk
    {
        public string Content { get; }
        /// <summary>
        /// Chunk locations relative to the source document.
        /// </summary>
        public IReadOnlyCollection<ChunkLocation> Locations { get => _locations; }
        public int TokenCount { get; }

        public string? Context { get; }

        private SortedSet<ChunkLocation> _locations;
        private static readonly ChunkLocationComparer _locationComparer = new ChunkLocationComparer();

        public Chunk(string content, IEnumerable<ChunkLocation> locations, int tokenCount, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            if (tokenCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must be greater than zero.");
            if (!locations.Any())
                throw new ArgumentException("At least one chunk location must be provided.", nameof(locations));

            Content = content;
            _locations = new SortedSet<ChunkLocation>(locations, _locationComparer);
            TokenCount = tokenCount;
            Context = context;
        }
    }

    public struct ChunkLocation
    {
        public int Start { get; }
        public int End => Start + Length;
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

    sealed class ChunkLocationComparer : IComparer<ChunkLocation>
    {
        public int Compare(ChunkLocation x, ChunkLocation y)
        {
            if (x.Start != y.Start)
            {
                return x.Start.CompareTo(y.Start);
            }
            return x.Length.CompareTo(y.Length);
        }
    }
}
