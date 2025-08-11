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

public class MarkItDownReader : DocumentReader
{
    private readonly string _exePath;

    public MarkItDownReader(string exePath = "markitdown")
    {
        _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
    }

    public override async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string outputPath = GetTempFilePath();
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);


        string outputContent = "";
        try
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"MarkItDown process failed with exit code {process.ExitCode}.");
                }
            }

            outputContent = await File.ReadAllTextAsync(outputPath, cancellationToken);
        }
        finally
        {
            File.Delete(outputPath); // Clean up the temporary output file as soon as we are done with it.
        }

        // Markdig's "UseAdvancedExtensions" option includes many common extensions beyond
        // CommonMark, such as citations, figures, footnotes, grid tables, mathematics
        // task lists, diagrams, and more.
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        MarkdownDocument markdownDocument = Markdown.Parse(outputContent, pipeline);
        return Map(markdownDocument, outputContent);
    }

    public override async Task<Document> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.IsFile)
        {
            return await ReadAsync(source.LocalPath, cancellationToken);
        }

        HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.GetAsync(source, cancellationToken);
        response.EnsureSuccessStatusCode();

        string inputFilePath = GetTempFilePath();
        using (FileStream inputFile = new(inputFilePath, FileMode.Open, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.Asynchronous))
        {
            await response.Content.CopyToAsync(inputFile, cancellationToken);
        }

        try
        {
            return await ReadAsync(inputFilePath, cancellationToken);
        }
        finally
        {
            File.Delete(inputFilePath);
        }
    }

    private static string GetTempFilePath() => Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

    private Document Map(MarkdownDocument markdownDocument, string outputContent)
    {
        Section rootSection = new()
        {
            Markdown = outputContent
        };
        Document result = new()
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

            Element? element = block switch
            {
                LeafBlock leafBlock => MapLeafBlockToElement(leafBlock, previousWasBreak),
                ListBlock listBlock => MapListBlock(listBlock, previousWasBreak, outputContent),
                QuoteBlock quoteBlock => MapQuoteBlock(quoteBlock, previousWasBreak, outputContent),
                Markdig.Extensions.Tables.Table table => new Table()
                {
                    // TODO: provide Table design and map all data
                },
                _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
            };

            element.Markdown = outputContent.Substring(block.Span.Start, block.Span.Length);
            rootSection.Elements.Add(element);
            previousWasBreak = false;
        }

        return result;
    }

    private static Element MapLeafBlockToElement(LeafBlock block, bool previousWasBreak)
        => block switch
        {
            HeadingBlock heading => new Header
            {
                Text = GetText(heading.Inline),
                Level = heading.Level
            },
            ParagraphBlock footer when previousWasBreak => new Footer
            {
                Text = GetText(footer.Inline),
            },
            ParagraphBlock paragraph => new Paragraph
            {
                Text = GetText(paragraph.Inline),
            },
            CodeBlock codeBlock => new Paragraph
            {
                Text = GetText(codeBlock.Inline),
            },
            _ => throw new NotSupportedException($"Block type '{block.GetType().Name}' is not supported.")
        };

    private static Section MapListBlock(ListBlock listBlock, bool previousWasBreak, string outputContent)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent lists.
        Section list = new();
        foreach (ListItemBlock item in listBlock) // can this hard cast fail for quote of lists?
        {
            foreach (LeafBlock child in item)
            {
                Element element = MapLeafBlockToElement(child, previousWasBreak);
                element.Markdown = outputContent.Substring(child.Span.Start, child.Span.Length);
                list.Elements.Add(element);
            }
        }

        return list;
    }

    private static Section MapQuoteBlock(QuoteBlock quoteBlock, bool previousWasBreak, string outputContent)
    {
        // So far Sections were only pages (LP) or sections for ADI. Now they can also represent quotes.
        Section quote = new();
        foreach (LeafBlock child in quoteBlock)
        {
            Element element = MapLeafBlockToElement(child, previousWasBreak);
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
