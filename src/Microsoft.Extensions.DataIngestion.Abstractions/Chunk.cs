// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    public sealed class Chunk : IContentElement
    {
        public string Text { get; }
        public string Markdown { get; }
        public int? PageNumber { get; }
        public string? Context { get; }

        public Chunk(string text, string markdown = null, int? pageNumber = null, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Chunk text cannot be null or empty.", nameof(text));
            }

            Text = text;
            Markdown = markdown;
            PageNumber = pageNumber;
            Context = context;
        }

        public Chunk(IContentElement element) : this(element.Text, element.Markdown, element.PageNumber) { }

        public Chunk(IEnumerable<IContentElement> elements)
        {
            if (elements == null || !elements.Any())
            {
                throw new ArgumentException("Elements collection cannot be null or empty.", nameof(elements));
            }
            Text = string.Join("", elements.Select(e => e.Text));
            Markdown = Utils.ConcatMarkdown(elements);
            PageNumber = elements.FirstOrDefault()?.PageNumber;
        }
    }
}
