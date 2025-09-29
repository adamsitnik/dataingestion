// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));

            List<DocumentChunk> chunks = document.Sections.Select(ProcessSection)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => new DocumentChunk(text))
                .ToList();

            return Task.FromResult(chunks);
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
