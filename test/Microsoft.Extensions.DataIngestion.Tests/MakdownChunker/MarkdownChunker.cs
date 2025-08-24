// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public sealed class MarkdownChunker : DocumentChunker
    {
        private readonly int _headerLevelToSplitOn;
        private readonly bool _stripHeaders;


        public MarkdownChunker(MarkdownHeaderLevel HeaderLevelToSplitOn = MarkdownHeaderLevel.Header3, bool StripHeaders = true)
        {
            _headerLevelToSplitOn = (int)HeaderLevelToSplitOn;
            _stripHeaders = StripHeaders;
        }

        public override ValueTask<List<Chunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            string markdown = document.Markdown.ReplaceLineEndings();
            string[] lines = markdown.Split(Environment.NewLine);

            var lineStack = new Stack<string>(lines.Reverse());
            return new ValueTask<List<Chunk>>(ParseLevel(lineStack, 1));
        }

        private List<Chunk> ParseLevel(Stack<string> lines, int markdownHeaderLevel, string context = null, string lastHeader = null)
        {
            List<Chunk> chunks = new List<Chunk>();

            StringBuilder sb = new StringBuilder();

            while (lines.Any())
            {
                string line = lines.Pop();

                int leadingHashes = CountLeadingHashes(line.Trim());
                if (leadingHashes == 0 || leadingHashes > _headerLevelToSplitOn)
                {
                    sb.AppendLine(line);
                }
                else
                {
                    Chunk? currentChunk = CreateChunk(sb, context, lastHeader);
                    if (currentChunk is not null)
                    {
                        chunks.Add(currentChunk);
                    }
                    sb.Clear();

                    if (leadingHashes == markdownHeaderLevel)
                    {
                        lastHeader = line;
                    }
                    else if (leadingHashes < markdownHeaderLevel)
                    {
                        lines.Push(line);
                        return chunks;
                    }
                    else
                    {
                        string newContext = StringyfyContext(context, lastHeader);
                        chunks.AddRange(ParseLevel(lines, markdownHeaderLevel + 1, newContext, line));
                    }

                }
            }

            Chunk? chunk = CreateChunk(sb, context, lastHeader);
            if (chunk is not null)
            {
                chunks.Add(chunk);
            }

            return chunks;
        }

        private static string StringyfyContext(string? context, string? lastHeader)
        {
            return String.Join(';', new[] { context, lastHeader }.Where(x => x is not null));
        }

        private Chunk? CreateChunk(StringBuilder content, string context, string? header)
        {
            context = StringyfyContext(context, header);
            if (!_stripHeaders)
            {
                content.Insert(0, context);
            }
            string textContent = content.ToString();
            if (string.IsNullOrWhiteSpace(textContent))
                return null;
            return new Chunk(textContent, context: context);
        }

        private static int CountLeadingHashes(string line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            return line.TakeWhile(c => c == '#').Count();
        }
    }

    public enum MarkdownHeaderLevel
    {
        Header1 = 1,
        Header2 = 2,
        Header3 = 3,
        Header4 = 4,
        Header5 = 5,
        Header6 = 6
    }
}
