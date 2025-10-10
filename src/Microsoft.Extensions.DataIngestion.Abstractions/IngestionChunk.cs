// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Extensions.DataIngestion;

[DebuggerDisplay("{Content}")]
public sealed class IngestionChunk<T>
{
    private Dictionary<string, object>? _metadata;

    public T Content { get; }

    public IngestionDocument Document { get; }

    public int? TokenCount { get; }

    public string? Context { get; }

    public bool HasMetadata => _metadata?.Count > 0;

    public Dictionary<string, object> Metadata => _metadata ??= new();

    public IngestionChunk(T content, IngestionDocument document, int? tokenCount = null, string? context = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        if (typeof(T) == typeof(string))
        {
            if (string.IsNullOrWhiteSpace((string)(object)content))
            {
                throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
            }
        }
        if (tokenCount.HasValue && tokenCount.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must be greater than zero.");
        }

        Content = content;
        Document = document ?? throw new ArgumentNullException(nameof(document));
        TokenCount = tokenCount;
        Context = context;
    }
}
