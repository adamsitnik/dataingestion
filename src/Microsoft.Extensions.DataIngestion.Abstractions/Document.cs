// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion
{
    public sealed class Document
    {
        private string? _markdown;

        public List<Section> Sections { get; } = [];

        public string Markdown
        {
            get => _markdown ??= string.Join("", Sections.Select(section => section.Markdown));
            set => _markdown = value;
        }

        public override string ToString() => Markdown;
    }

    public abstract class Element
    {
        public string Text { get; set; } = string.Empty;

        public virtual string Markdown { get; set; } = string.Empty;

        public int? PageNumber { get; set; }

        public override string ToString() => $"{GetType().Name}: {Markdown}";
    }

    /// <summary>
    /// A section can be just a page or a logical grouping of elements in a document.
    /// </summary>
    public sealed class Section : Element
    {
        private string? _markdown;

        public List<Element> Elements { get; } = [];

        public override string Markdown
        {
            get => _markdown ??= string.Join("", Elements.Select(e => e.Markdown));
            set => _markdown = value;
        }
    }

    public sealed class Paragraph : Element
    {
    }

    public sealed class Header : Element
    {
        public int? Level { get; set; }
    }

    public sealed class Footer : Element
    {
    }

    public sealed class Table : Element
    {
    }
}
