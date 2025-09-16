// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    /// <summary>
    /// Processes a Markdown document and splits it into smaller chunks based on specified header levels.
    /// </summary>
    /// <remarks>This class is designed to parse a Markdown document and divide it into logical chunks based
    /// on the specified header level. Each chunk represents a section of the document, and the headers can be
    /// optionally stripped from the output. The splitting behavior is controlled by the header level. </remarks>
    public sealed class MarkdownChunker : DocumentChunker
    {
        private readonly int _headerLevelToSplitOn;
        private readonly bool _stripHeaders;

        public MarkdownChunker(int headerLevelToSplitOn = 3, bool stripHeaders = true)
        {
            _headerLevelToSplitOn = headerLevelToSplitOn;
            _stripHeaders = stripHeaders;
        }

        public override ValueTask<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            IEnumerable<DocumentElement> elements = document.Sections.SelectMany(section => section.Elements).Reverse();
            var sectionStack = new Stack<DocumentElement>(elements);

            return new ValueTask<List<DocumentChunk>>(ParseLevel(sectionStack, 1));
        }

        private List<DocumentChunk> ParseLevel(Stack<DocumentElement> lines, int markdownHeaderLevel, string? context = null, string? lastHeader = null)
        {
            List<DocumentChunk> chunks = new List<DocumentChunk>();

            StringBuilder sb = new StringBuilder();

            while (lines.Any())
            {
                DocumentElement element = lines.Pop();

                int headerLevel = element is DocumentHeader header ? header.Level.GetValueOrDefault(0) : 0;
                if (headerLevel == 0 || headerLevel > _headerLevelToSplitOn)
                {
                    sb.AppendLine(element.Markdown);
                }
                else
                {
                    DocumentChunk? currentChunk = CreateChunk(sb, context, lastHeader);
                    if (currentChunk is not null)
                    {
                        chunks.Add(currentChunk);
                    }
                    sb.Clear();

                    if (headerLevel == markdownHeaderLevel)
                    {
                        lastHeader = element.Markdown;
                    }
                    else if (headerLevel < markdownHeaderLevel)
                    {
                        lines.Push(element);
                        return chunks;
                    }
                    else
                    {
                        string newContext = StringyfyContext(context, lastHeader);
                        chunks.AddRange(ParseLevel(lines, markdownHeaderLevel + 1, newContext, element.Markdown));
                    }

                }
            }

            DocumentChunk? chunk = CreateChunk(sb, context, lastHeader);
            if (chunk is not null)
            {
                chunks.Add(chunk);
            }

            return chunks;
        }

        private static string StringyfyContext(string? context, string? lastHeader)
        {
            return string.Join(";", new[] { context, lastHeader }.Where(x => x is not null));
        }

        private DocumentChunk? CreateChunk(StringBuilder content, string context, string? header)
        {
            context = StringyfyContext(context, header);
            if (!_stripHeaders)
            {
                content.Insert(0, context);
            }
            string textContent = content.ToString();
            if (string.IsNullOrWhiteSpace(textContent))
                return null;
            return new DocumentChunk(textContent, context: context);
        }
    }
}
