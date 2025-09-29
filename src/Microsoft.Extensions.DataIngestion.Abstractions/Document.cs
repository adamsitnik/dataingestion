// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion;

[DebuggerDisplay("{Markdown}")]
public sealed class Document : IEnumerable<DocumentElement>
{
    private string? _markdown;

    public Document(string identifier)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }

    public List<DocumentSection> Sections { get; } = [];

    public string Identifier { get; }

    public string Markdown
    {
        get
        {
            // In case there are no Sections, we don't want to cache an empty string.
            if (string.IsNullOrEmpty(_markdown))
            {
                _markdown = string.Join("", Sections.Select(section => section.Markdown));
            }

            return _markdown!;
        }
        set => _markdown = value;
    }

    /// <summary>
    /// Iterate over all elements in the document, including those in nested sections.
    /// </summary>
    /// <remarks>
    /// Sections themselves are not included.
    /// </remarks>
    public IEnumerator<DocumentElement> GetEnumerator()
    {
        Stack<DocumentElement> elementsToProcess = new();

        for (int sectionIndex = Sections.Count - 1; sectionIndex >= 0; sectionIndex--)
        {
            elementsToProcess.Push(Sections[sectionIndex]);
        }

        while (elementsToProcess.Count > 0)
        {
            DocumentElement currentElement = elementsToProcess.Pop();

            if (currentElement is not DocumentSection nestedSection)
            {
                yield return currentElement;
            }
            else
            {
                for (int i = nestedSection.Elements.Count - 1; i >= 0; i--)
                {
                    elementsToProcess.Push(nestedSection.Elements[i]);
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[DebuggerDisplay("{GetType().Name}: {Markdown}")]
public abstract class DocumentElement
{
    protected string _markdown;

    protected DocumentElement(string markdown)
    {
        _markdown = string.IsNullOrEmpty(markdown) ? throw new ArgumentNullException(nameof(markdown)) : markdown;
    }

    protected internal DocumentElement() => _markdown = null!;

    private Dictionary<string, object?>? _metadata;

    public string Text { get; set; } = string.Empty;

    public virtual string Markdown => _markdown;

    public int? PageNumber { get; set; }

    public Dictionary<string, object?> Metadata => _metadata ??= new();
}

/// <summary>
/// A section can be just a page or a logical grouping of elements in a document.
/// </summary>
public sealed class DocumentSection : DocumentElement
{
    public DocumentSection(string markdown) : base(markdown)
    {
    }

    // the user is not providing the Markdown, we will compute it from the elements
    public DocumentSection() : base()
    {
    }

    public List<DocumentElement> Elements { get; } = [];

    public override string Markdown
    {
        get
        {
            // In case there are no Elements, we don't want to cache an empty string.
            if (string.IsNullOrEmpty(_markdown))
            {
                _markdown = string.Join(Environment.NewLine, Elements.Select(e => e.Markdown));
            }

            return _markdown;
        }
    }
}

public sealed class DocumentParagraph : DocumentElement
{
    public DocumentParagraph(string markdown) : base(markdown)
    {
    }
}

public sealed class DocumentHeader : DocumentElement
{
    public DocumentHeader(string markdown) : base(markdown)
    {
    }

    public int? Level { get; set; }
}

public sealed class DocumentFooter : DocumentElement
{
    public DocumentFooter(string markdown) : base(markdown)
    {
    }
}

public sealed class DocumentTable : DocumentElement
{
    // So far, we only support Markdown representation of the table
    // because "LLMs speak Markdown" and there was no need to access
    // individual rows/columns/cells.
    public DocumentTable(string markdown) : base(markdown)
    {
    }
}

public sealed class DocumentImage : DocumentElement
{
    public DocumentImage(string markdown) : base(markdown)
    {
    }

    public BinaryData? Content { get; set; }

    public string? MediaType { get; set; }

    /// <summary>
    /// Alternative text is a brief, descriptive text that explains the content, context, or function of an image when the image cannot be displayed or accessed.
    /// This property can be used when generating the embedding for the image that is part of larger chunk.
    /// </summary>
    public string? AlternativeText { get; set; }
}
