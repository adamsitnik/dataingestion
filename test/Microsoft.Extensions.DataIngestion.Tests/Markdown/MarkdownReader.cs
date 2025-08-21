// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public sealed class MarkdownReader : DocumentReader
{
    public override async Task<Document> ReadAsync(string filePath, string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        string fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Parse(fileContent, identifier);
    }

    public override async Task<Document> ReadAsync(Uri source, string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.GetAsync(source, cancellationToken);
        response.EnsureSuccessStatusCode();

        string fileContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(fileContent, identifier);
    }

    internal static Document Parse(string fileContent, string identifier)
    {
        // Markdig's "UseAdvancedExtensions" option includes many common extensions beyond
        // CommonMark, such as citations, figures, footnotes, grid tables, mathematics
        // task lists, diagrams, and more.
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        MarkdownDocument markdownDocument = Markdown.Parse(fileContent, pipeline);
        return Map(markdownDocument, fileContent, identifier);
    }

    private static Document Map(MarkdownDocument markdownDocument, string outputContent, string identifier)
    {
        DocumentSection rootSection = new()
        {
            Markdown = outputContent
        };
        Document result = new(identifier)
        {
            Markdown = outputContent,
            Sections = { rootSection }
        };

        bool previousWasBreak = false;
        foreach (Block block in markdownDocument)
        {
            if (block is ThematicBreakBlock breakBlock)
            {
                // We have encountered a thematic break (horizontal rule): ----------- etc.
                previousWasBreak = true;
                continue;
            }

            if (block is LinkReferenceDefinitionGroup linkReferenceGroup)
            {
                continue; // TODO: Handle link reference definitions if needed.
            }

            if (block is LeafBlock emptyLeafBlock && (emptyLeafBlock.Inline is null || emptyLeafBlock.Inline.FirstChild is null))
            {
                continue; // Block with no text. Sample: QuoteBlock the next block is a quote.
            }

            DocumentElement? element = block switch
            {
                LeafBlock leafBlock => MapLeafBlockToElement(leafBlock, previousWasBreak),
                ListBlock listBlock => MapListBlock(listBlock, previousWasBreak, outputContent),
                QuoteBlock quoteBlock => MapQuoteBlock(quoteBlock, previousWasBreak, outputContent),
                Markdig.Extensions.Tables.Table table => new DocumentTable()
                {
                    // TODO: provide DocumentTable design and map all data
                },
                _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
            };

            element.Markdown = outputContent.Substring(block.Span.Start, block.Span.Length);
            rootSection.Elements.Add(element);
            previousWasBreak = false;
        }

        return result;
    }

    private static DocumentElement MapLeafBlockToElement(LeafBlock block, bool previousWasBreak)
        => block switch
        {
            HeadingBlock heading => new DocumentHeader
            {
                Text = GetText(heading.Inline),
                Level = heading.Level
            },
            ParagraphBlock footer when previousWasBreak => new DocumentFooter
            {
                Text = GetText(footer.Inline),
            },
            ParagraphBlock image when image.Inline!.FirstChild is LinkInline link && link.IsImage => new DocumentImage
            {
                Text = GetText(image.Inline),
                Content = link.Url is not null && link.Url.StartsWith("data:image/png;base64,", StringComparison.Ordinal)
                    ? BinaryData.FromBytes(Convert.FromBase64String(link.Url.Substring("data:image/png;base64,".Length)))
                    : throw new NotSupportedException() // we may implement it in the future if needed
            },
            ParagraphBlock paragraph => new DocumentParagraph
            {
                Text = GetText(paragraph.Inline),
            },
            CodeBlock codeBlock => new DocumentParagraph
            {
                Text = GetText(codeBlock.Inline),
            },
            _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
        };

    private static DocumentSection MapListBlock(ListBlock listBlock, bool previousWasBreak, string outputContent)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent lists.
        DocumentSection list = new();
        foreach (ListItemBlock item in listBlock) // can this hard cast fail for quote of lists?
        {
            foreach (LeafBlock child in item)
            {
                DocumentElement element = MapLeafBlockToElement(child, previousWasBreak);
                element.Markdown = outputContent.Substring(child.Span.Start, child.Span.Length);
                list.Elements.Add(element);
            }
        }

        return list;
    }

    private static DocumentSection MapQuoteBlock(QuoteBlock quoteBlock, bool previousWasBreak, string outputContent)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent quotes.
        DocumentSection quote = new();
        foreach (LeafBlock child in quoteBlock)
        {
            DocumentElement element = MapLeafBlockToElement(child, previousWasBreak);
            element.Markdown = outputContent.Substring(child.Span.Start, child.Span.Length);
            quote.Elements.Add(element);
        }

        return quote;
    }

    private static string GetText(ContainerInline? containerInline)
    {
        Debug.Assert(containerInline != null, "ContainerInline should not be null here.");
        Debug.Assert(containerInline.FirstChild != null, "FirstChild should not be null here.");

        if (ReferenceEquals(containerInline.FirstChild, containerInline.LastChild))
        {
            // If there is only one child, return its text.
            return containerInline.FirstChild.ToString()!;
        }

        StringBuilder content = new(100);
        foreach (Inline inline in containerInline)
        {
            if (inline is LiteralInline literalInline)
            {
                content.Append(literalInline.Content);
            }
            else if (inline is LineBreakInline)
            {
                // TODO adsitnik design: should each reader accept the NewLine property from the configuration?
                // So parsing the same document on different platforms would yield the same result?
                content.AppendLine(); // Append a new line for line breaks
            }
            else if (inline is ContainerInline another)
            {
                content.Append(GetText(another)); // recursion!
            }
            else
            {
                // TODO: study EmphasisInline and LinkInline to see how to handle them properly.
                content.Append(inline.ToString()); // Fallback for other inline types
            }
        }

        return content.ToString();
    }
}
