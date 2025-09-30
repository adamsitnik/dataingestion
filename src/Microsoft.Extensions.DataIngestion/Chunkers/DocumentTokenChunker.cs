// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    /// <summary>
    /// Processes a document by tokenizing its content and dividing it into overlapping chunks of tokens.
    /// </summary>
    /// <remarks>This class uses a tokenizer to convert the document's content into tokens and then splits the
    /// tokens into chunks of a specified size, with a configurable overlap between consecutive chunks. The resulting
    /// chunks are returned as a list of <see cref="Chunk"/> objects.</remarks>
    public sealed class DocumentTokenChunker : IDocumentChunker
    {
        private readonly Tokenizer _tokenizer;
        private readonly int _maxTokensPerChunk;
        private readonly int _chunkOverlap;

        public DocumentTokenChunker(Tokenizer tokenizer, int maxTokensPerChunk, int chunkOverlap)
        {
            if (chunkOverlap >= maxTokensPerChunk)
                throw new ArgumentException("Chunk overlap must be less than chunk size.", nameof(chunkOverlap));

            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _maxTokensPerChunk = maxTokensPerChunk > 0 ? maxTokensPerChunk : throw new ArgumentOutOfRangeException(nameof(maxTokensPerChunk));
            _chunkOverlap = chunkOverlap >= 0 ? chunkOverlap : throw new ArgumentOutOfRangeException(nameof(chunkOverlap));
        }

        internal List<DocumentChunk> ProcessText(string text, string? context = null)
        {
            int[] tokens = _tokenizer.EncodeToIds(text).ToArray();
            List<ArraySegment<int>> tokenGroups = CreateGroups(tokens);
            return tokenGroups.Select(g => GroupToChunk(g, context)).ToList();
        }

        public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            return Task.FromResult(ProcessText(document.Markdown));
        }

        private List<ArraySegment<int>> CreateGroups(int[] tokens)
        {
            List<ArraySegment<int>> groups = new List<ArraySegment<int>>();
            for (int i = 0; i < tokens.Length; i += (_maxTokensPerChunk - _chunkOverlap))
            {
                int count = Math.Min(_maxTokensPerChunk, tokens.Length - i);
                groups.Add(new ArraySegment<int>(tokens, i, count));
            }
            return groups;
        }

        private DocumentChunk GroupToChunk(ArraySegment<int> tokenGroup, string? context = null)
        {
            string text = _tokenizer.Decode(tokenGroup);
            return new DocumentChunk(text, tokenGroup.Count, context);
        }
    }
}
