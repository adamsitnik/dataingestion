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
        private readonly int _chunkSize;
        private readonly int _chunkOverlap;

        public DocumentTokenChunker(Tokenizer tokenizer, int chunkSize, int chunkOverlap)
        {
            if (chunkOverlap >= chunkSize)
                throw new ArgumentException("Chunk overlap must be less than chunk size.", nameof(chunkOverlap));

            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _chunkSize = chunkSize > 0 ? chunkSize : throw new ArgumentOutOfRangeException(nameof(chunkSize));
            _chunkOverlap = chunkOverlap >= 0 ? chunkOverlap : throw new ArgumentOutOfRangeException(nameof(chunkOverlap));
        }

        public Task<List<DocumentChunk>> ProcessAsync(string text, CancellationToken cancellationToken = default)
        {
            int[] tokens = _tokenizer.EncodeToIds(text).ToArray();
            List<ArraySegment<int>> tokenGroups = CreateGroups(tokens);
            List<DocumentChunk> textGroups = tokenGroups.Select(GroupToChunk).ToList();

            return Task.FromResult(textGroups);
        }

        public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            return ProcessAsync(document.Markdown);
        }

        private List<ArraySegment<int>> CreateGroups(int[] tokens)
        {
            List<ArraySegment<int>> groups = new List<ArraySegment<int>>();
            for (int i = 0; i < tokens.Length; i += (_chunkSize - _chunkOverlap))
            {
                int count = Math.Min(_chunkSize, tokens.Length - i);
                groups.Add(new ArraySegment<int>(tokens, i, count));
            }
            return groups;
        }

        private DocumentChunk GroupToChunk(ArraySegment<int> tokenGroup)
        {
            string text = _tokenizer.Decode(tokenGroup);
            return new DocumentChunk(text, tokenGroup.Count);
        }
    }
}
