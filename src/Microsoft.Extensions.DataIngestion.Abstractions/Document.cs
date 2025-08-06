// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.DataIngestion
{
    public sealed class Document
    {
        public List<Section> Sections { get; } = [];

        public override string ToString() => string.Join("-----", Sections);
    }

    public abstract class Element
    {
        private string? _text;

        public string Text
        {
            get => _text ?? GetDefaultText();
            set => _text = value;
        }

        public string? Markdown { get; set; }

        public int? PageNumber { get; set; }

        private protected virtual string GetDefaultText() => string.Empty;

        public override string ToString() => $"{GetType().Name}: {Text}";
    }

    /// <summary>
    /// A section can be just a page or a logical grouping of elements in a document.
    /// </summary>
    public sealed class Section : Element
    {
        public List<Element> Elements { get; } = [];

        private protected override string GetDefaultText() => string.Join(Environment.NewLine, Elements);
    }

    public sealed class Paragraph : Element
    {
    }

    // Should Header derive from Paragraph or Element?
    public sealed class Header : Element
    {
        public int? Level { get; set; }
    }

    // Should Footer derive from Paragraph or Element?
    public sealed class Footer : Element
    {
    }

    public sealed class Table : Element
    {
    }
}
