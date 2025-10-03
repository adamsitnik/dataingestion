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
        private readonly DocumentTokenChunker _documentTokenChunker;
        public SectionChunker(Tokenizer tokenizer, int maxTokensPerChunk, int chunkOverlap)
        {
            if (maxTokensPerChunk <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokensPerChunk));

            _documentTokenChunker = new(tokenizer, maxTokensPerChunk, chunkOverlap);
        }

        public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            List<DocumentChunk> chunks = document.Sections.Select(ProcessSection)
                .Where(x => !string.IsNullOrWhiteSpace(x.content))
                .SelectMany(x =>_documentTokenChunker.ProcessText(x.content, x.context))
                .ToList();

            return Task.FromResult(chunks);
        }

        private (string content, string? context) ProcessSection(DocumentSection section)
        {
            StringBuilder sectionText = new();
            foreach (var element in section.Elements)
            {
                sectionText.AppendLine(GetSemanticContent(element)); // No special handling for nested sections
            }

            string? context = null;
            DocumentElement? firstElement = section.Elements.FirstOrDefault();
            if (firstElement is DocumentHeader)
            {
                context = firstElement.Markdown;
            }

            return (sectionText.ToString(), context);
        }
    }
}
