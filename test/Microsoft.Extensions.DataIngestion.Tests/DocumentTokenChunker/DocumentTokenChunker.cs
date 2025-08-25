// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    internal class DocumentTokenChunker : DocumentChunker
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

        public override ValueTask<List<Chunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            var tokens = _tokenizer.EncodeToIds(document.Markdown);
            var token_groups = CreateGroups(tokens);
            List<Chunk> text_groups = token_groups.Select(GroupToChunk).ToList();


            return new ValueTask<List<Chunk>>(text_groups);
        }
        // Additional methods for chunking documents would go here.

        private List<List<int>> CreateGroups(IReadOnlyCollection<int> tokens)
        {
            List<List<int>> groups = new List<List<int>>();
            for (int i = 0; i < tokens.Count; i += (_chunkSize - _chunkOverlap))
            {
                var chunk = tokens.Skip(i).Take(_chunkSize).ToList();
                if (chunk.Any())
                {
                    groups.Add(chunk);
                }
            }
            return groups;
        }

        private Chunk GroupToChunk(List<int> tokenGroup)
        {
            string text = _tokenizer.Decode(tokenGroup);
            return new Chunk(text, tokenGroup.Count);
        }
    }
}
