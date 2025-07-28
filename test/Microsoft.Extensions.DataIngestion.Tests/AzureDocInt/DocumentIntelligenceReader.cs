// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public sealed class DocumentIntelligenceReader : DocumentReader
    {
        private readonly DocumentIntelligenceClient _client;
        private readonly string _modelName;

        /// <param name="modelName">Unique document model name (<see cref="https://learn.microsoft.com/azure/ai-services/document-intelligence/overview#document-analysis-models"/>).</param>
        public DocumentIntelligenceReader(DocumentIntelligenceClient client, string modelName = "prebuilt-layout")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _modelName = string.IsNullOrEmpty(modelName) ? throw new ArgumentNullException(nameof(modelName)) : modelName;
        }

        public override async Task<Document> ReadAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, _modelName, uri, cancellationToken);

            return Map(operation.Value);
        }

        public override async Task<Document> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BinaryData binaryData = await BinaryData.FromStreamAsync(stream, cancellationToken);
            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, _modelName, binaryData, cancellationToken);

            return Map(operation.Value);
        }

        private static Document Map(AnalyzeResult operation)
        {
            Document result = new();

            foreach (var page in operation.Pages)
            {
                // TODO adsitnik: map everything, design the initial structure of the Document class
            }

            return result;
        }
    }
}
