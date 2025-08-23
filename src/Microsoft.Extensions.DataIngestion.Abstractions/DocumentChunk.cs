// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Extensions.DataIngestion;

[DebuggerDisplay("{Content}")]
public sealed class DocumentChunk
{
    private Dictionary<string, object?>? _metadata;

    public string Content { get; }

    public int? TokenCount { get; }

    public string? Context { get; }

    public Dictionary<string, object?> Metadata => _metadata ??= new();

    public DocumentChunk(string content, int? tokenCount = null, string? context = null)
    {
        public string Content { get; }
        public int? TokenCount { get; }

        public string? Context { get; }

        public Chunk(string content, int? tokenCount = null, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            if (tokenCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must be greater than zero.");

            Content = content;
            TokenCount = tokenCount;
            Context = context;
        }
    }
}
