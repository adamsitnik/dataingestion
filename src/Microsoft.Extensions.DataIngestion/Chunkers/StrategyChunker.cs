// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices; // Add this at the top

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public sealed class StrategyChunker : IngestionChunker<string>
    {
        private readonly TextSplittingStrategy _strategy;
        private readonly int _maxTokenCount;
        public StrategyChunker(TextSplittingStrategy strategy, int maxTokenCount)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _maxTokenCount = maxTokenCount;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
            IngestionDocument document,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string markdown = ChunkingHelpers.GetDocumentMarkdown(document);
            if (string.IsNullOrEmpty(markdown))
            {
                yield break;
            }
            List<int> splitIndices = _strategy.GetSplitIndices(markdown.AsSpan(), _maxTokenCount);

            splitIndices.Insert(0, 0);
            splitIndices.Add(markdown.Length);

            List<IngestionChunk<string>> chunks = [];
            for (int i = 0; i < splitIndices.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int start = splitIndices[i];
                int end = splitIndices[i + 1];
                string chunkContent = markdown.Substring(start, end - start);
                yield return new IngestionChunk<string>(chunkContent, document);
            }
        }

    }
}
