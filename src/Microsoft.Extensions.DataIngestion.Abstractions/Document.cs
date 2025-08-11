// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    [DebuggerDisplay("{Markdown}")]
    public sealed class Document
    {
        private string? _markdown;

        public List<DocumentSection> Sections { get; } = [];

        public string Markdown
        {
            get => _markdown ??= string.Join("", Sections.Select(section => section.Markdown));
            set => _markdown = value;
        }
    }

    [DebuggerDisplay("{GetType().Name}: {Markdown}")]
    public abstract class DocumentElement
    {
        public string Text { get; set; } = string.Empty;

        public virtual string Markdown { get; set; } = string.Empty;

        public int? PageNumber { get; set; }
    }

    /// <summary>
    /// A section can be just a page or a logical grouping of elements in a document.
    /// </summary>
    public sealed class DocumentSection : DocumentElement
    {
        private string? _markdown;

        public List<DocumentElement> Elements { get; } = [];

        public override string Markdown
        {
            get => _markdown ??= string.Join("", Elements.Select(e => e.Markdown));
            set => _markdown = value;
        }
    }

    public sealed class DocumentParagraph : DocumentElement
    {
    }

    public sealed class DocumentHeader : DocumentElement
    {
        public int? Level { get; set; }
    }

    public sealed class DocumentFooter : DocumentElement
    {
    }

    public sealed class DocumentTable : DocumentElement
    {
    }
}
