// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    public sealed class Chunk : IContentElement
    {
        public string Markdown { get; }
        public int? PageNumber { get; }
        public string? Context { get; }

        public Chunk(string markdown = null, int? pageNumber = null, string? context = null)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                throw new ArgumentException("Chunk content cannot be null or empty.", nameof(markdown));
            }

            Markdown = markdown;
            PageNumber = pageNumber;
            Context = context;
        }

        public Chunk(IContentElement element) : this(element.Markdown, element.PageNumber) { }

        public Chunk(IEnumerable<IContentElement> elements)
        {
            if (elements == null || !elements.Any())
            {
                throw new ArgumentException("Elements collection cannot be null or empty.", nameof(elements));
            }
            Markdown = Utils.ConcatMarkdown(elements);
            PageNumber = elements.FirstOrDefault()?.PageNumber;
        }
    }
}
