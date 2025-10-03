// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Markdig;
using Markdig.Extensions.Tables;
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
        DocumentSection rootSection = new(outputContent);
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
                continue; // In the future, we might want to handle links differently.
            }

            if (IsEmptyBlock(block))
            {
                continue;
            }

            rootSection.Elements.Add(MapBlock(outputContent, previousWasBreak, block));
            previousWasBreak = false;
        }

        return result;
    }

    private static bool IsEmptyBlock(Block block) // Block with no text. Sample: QuoteBlock the next block is a quote.
        => block is LeafBlock emptyLeafBlock && (emptyLeafBlock.Inline is null || emptyLeafBlock.Inline.FirstChild is null);

    private static DocumentElement MapBlock(string outputContent, bool previousWasBreak, Block block)
    {
        string elementMarkdown = outputContent.Substring(block.Span.Start, block.Span.Length);

        DocumentElement element = block switch
        {
            LeafBlock leafBlock => MapLeafBlockToElement(leafBlock, previousWasBreak, elementMarkdown),
            ListBlock listBlock => MapListBlock(listBlock, previousWasBreak, outputContent, elementMarkdown),
            QuoteBlock quoteBlock => MapQuoteBlock(quoteBlock, previousWasBreak, outputContent, elementMarkdown),
            Table table => new DocumentTable(elementMarkdown, GetCells(table, outputContent)),
            _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
        };

        return element;
    }

    private static DocumentElement MapLeafBlockToElement(LeafBlock block, bool previousWasBreak, string elementMarkdown)
        => block switch
        {
            HeadingBlock heading => new DocumentHeader(elementMarkdown)
            {
                Text = GetText(heading.Inline),
                Level = heading.Level
            },
            ParagraphBlock footer when previousWasBreak => new DocumentFooter(elementMarkdown)
            {
                Text = GetText(footer.Inline),
            },
            ParagraphBlock image when image.Inline!.FirstChild is LinkInline link && link.IsImage => new DocumentImage(elementMarkdown)
            {
                // ![Alt text](data:image/png;base64,...)
                AlternativeText = link.FirstChild is LiteralInline literal ? literal.Content.ToString() : null,
                Content = link.Url is not null && link.Url.StartsWith("data:image/png;base64,", StringComparison.Ordinal)
                    ? Convert.FromBase64String(link.Url.Substring("data:image/png;base64,".Length))
                    : null, // we may implement it in the future if needed
                MediaType = link.Url is not null && link.Url.StartsWith("data:image/png;base64,", StringComparison.Ordinal)
                    ? "image/png"
                    : null // we may implement it in the future if needed
            },
            ParagraphBlock paragraph => new DocumentParagraph(elementMarkdown)
            {
                Text = GetText(paragraph.Inline),
            },
            CodeBlock codeBlock => new DocumentParagraph(elementMarkdown)
            {
                Text = GetText(codeBlock.Inline),
            },
            _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
        };

    private static DocumentSection MapListBlock(ListBlock listBlock, bool previousWasBreak, string outputContent, string listMarkdown)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent lists.
        DocumentSection list = new(listMarkdown);
        foreach (Block? item in listBlock)
        {
            if (item is not ListItemBlock listItemBlock)
            {
                continue;
            }

            foreach (Block? child in listItemBlock)
            {
                if (child is not LeafBlock leafBlock || IsEmptyBlock(leafBlock))
                {
                    continue; // Skip empty blocks in lists
                }

                string childMarkdown = outputContent.Substring(leafBlock.Span.Start, leafBlock.Span.Length);
                DocumentElement element = MapLeafBlockToElement(leafBlock, previousWasBreak, childMarkdown);
                list.Elements.Add(element);
            }
        }

        return list;
    }

    private static DocumentSection MapQuoteBlock(QuoteBlock quoteBlock, bool previousWasBreak, string outputContent, string elementMarkdown)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent quotes.
        DocumentSection quote = new(elementMarkdown);
        foreach (Block child in quoteBlock)
        {
            if (IsEmptyBlock(child))
            {
                continue; // Skip empty blocks in quotes
            }

            quote.Elements.Add(MapBlock(outputContent, previousWasBreak, child));
        }

        return quote;
    }

    private static string? GetText(ContainerInline? containerInline)
    {
        Debug.Assert(containerInline != null, "ContainerInline should not be null here.");
        Debug.Assert(containerInline.FirstChild != null, "FirstChild should not be null here.");

        if (ReferenceEquals(containerInline.FirstChild, containerInline.LastChild))
        {
            // If there is only one child, return its text.
            return containerInline.FirstChild.ToString();
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
                content.AppendLine(); // Append a new line for line breaks
            }
            else if (inline is ContainerInline another)
            {
                // EmphasisInline is also a ContainerInline, but it does not get any special treatment,
                // as we use raw text here (instead of a markdown, where emphasis can be expressed).
                content.Append(GetText(another));
            }
            else if (inline is CodeInline codeInline)
            {
                content.Append(codeInline.Content);
            }
            else
            {
                throw new NotSupportedException($"Inline type '{inline.GetType().Name}' is not supported.");
            }
        }

        return content.ToString();
    }

    private static string[,] GetCells(Table table, string outputContent)
    {
        int firstRowIndex = SkipFirstRow(table, outputContent) ? 1 : 0;
        string[,] cells = new string[table.Count - firstRowIndex, table.ColumnDefinitions.Count - 1];

        for (int rowIndex = firstRowIndex; rowIndex < table.Count; rowIndex++)
        {
            TableRow tableRow = (TableRow)table[rowIndex];
            int columnIndex = 0;
            for (int cellIndex = 0; cellIndex < tableRow.Count; cellIndex++)
            {
                TableCell tableCell = (TableCell)tableRow[cellIndex];
                string content = tableCell.Count switch
                {
                    0 => string.Empty,
                    1 => MapBlock(outputContent, previousWasBreak: false, tableCell[0]).Text ?? string.Empty,
                    _ => throw new NotSupportedException($"Cells with {tableCell.Count} elements are not supported.")
                };

                for (int columnSpan = 0; columnSpan < tableCell.ColumnSpan; columnSpan++, columnIndex++)
                {
                    // We are not using tableCell.ColumnIndex here as it defaults to -1 ;)
                    cells[rowIndex - firstRowIndex, columnIndex] = content;
                }
            }
        }

        return cells;

        // Some parsers like MarkItDown include a row with invalid markdown before the separator row:
        // |  |  |  |  |
        // | --- | --- | --- | --- |
        static bool SkipFirstRow(Table table, string outputContent)
        {
            if (table.Count > 0)
            {
                TableRow firstRow = (TableRow)table[0];
                for (int cellIndex = 0; cellIndex < firstRow.Count; cellIndex++)
                {
                    TableCell tableCell = (TableCell)firstRow[cellIndex];
                    if (!string.IsNullOrWhiteSpace(MapBlock(outputContent, previousWasBreak: false, tableCell[0]).Text))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
