// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    /// <summary>
    /// Processes a document by tokenizing its content and dividing it into overlapping chunks of tokens.
    /// </summary>
    /// <remarks>This class uses a tokenizer to convert the document's content into tokens and then splits the
    /// tokens into chunks of a specified size, with a configurable overlap between consecutive chunks. The resulting
    /// chunks are returned as a list of <see cref="Chunk"/> objects.</remarks>
    public sealed class DocumentTokenChunker : IngestionChunker<string>
    {
        private readonly Tokenizer _tokenizer;
        private readonly int _maxTokensPerChunk;
        private readonly int _chunkOverlap;

        public DocumentTokenChunker(IngestionChunkerOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _tokenizer = options.Tokenizer;
            _maxTokensPerChunk = options.MaxTokensPerChunk;
            _chunkOverlap = options.OverlapTokens;
        }

        public override IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(IngestionDocument document, CancellationToken cancellationToken = default)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string documentMarkdown = GetDocumentMarkdown(document);
            int[] tokens = _tokenizer.EncodeToIds(documentMarkdown).ToArray();
            List<ArraySegment<int>> tokenGroups = CreateGroups(tokens);

            return tokenGroups.Select(g => GroupToChunk(document, g)).ToAsyncEnumerable();
        }

        private List<ArraySegment<int>> CreateGroups(int[] tokens)
        {
            List<ArraySegment<int>> groups = [];
            for (int i = 0; i < tokens.Length; i += (_maxTokensPerChunk - _chunkOverlap))
            {
                int count = Math.Min(_maxTokensPerChunk, tokens.Length - i);
                groups.Add(new ArraySegment<int>(tokens, i, count));
            }
            return groups;
        }

        private IngestionChunk<string> GroupToChunk(IngestionDocument document, ArraySegment<int> tokenGroup)
        {
            string text = _tokenizer.Decode(tokenGroup);
            return new(text, document);
        }

        private static string GetDocumentMarkdown(IngestionDocument document)
        {
            StringBuilder sb = new();
            for (int i = 0; i < document.Sections.Count; i++)
            {
                sb.Append(document.Sections[i].GetMarkdown());
                if (i != document.Sections.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
