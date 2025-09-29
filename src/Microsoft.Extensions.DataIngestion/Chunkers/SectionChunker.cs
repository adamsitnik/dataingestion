// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.Extensions.DataIngestion.ElementUtils;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public class SectionChunker : IDocumentChunker
    {
        private DocumentTokenChunker? _documentTokenChunker;

        public SectionChunker() { }
        public SectionChunker(int maxTokensPerChunk, Tokenizer tokenizer)
        {
            if (maxTokensPerChunk <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokensPerChunk));

            _documentTokenChunker = new DocumentTokenChunker(tokenizer, maxTokensPerChunk, 0);
        }
        public async Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            IEnumerable<Task<List<DocumentChunk>>> chunkTasks = document.Sections
                .Select(ProcessSection)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(CreateChunks);

            var chunkLists = await Task.WhenAll(chunkTasks);
            return chunkLists.SelectMany(list => list).ToList();
        }

        private async Task<List<DocumentChunk>> CreateChunks(string content)
        {
            if (_documentTokenChunker is not null)
            {
                return await _documentTokenChunker.ProcessAsync(content);
            }
            return [new DocumentChunk(content)];
        }

        private string ProcessSection(DocumentSection section)
        {
            StringBuilder sectionText = new StringBuilder();
            foreach (var element in section.Elements)
            {
                sectionText.AppendLine(GetSemanticContent(element));
            }
            return sectionText.ToString();
        }
    }
}
