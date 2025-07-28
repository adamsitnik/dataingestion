// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Azure;
using Azure.AI.DocumentIntelligence;
using System;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public abstract class DocumentIntelligenceReaderTests : DocumentReaderConformanceTests
    {
        protected abstract string ModelName { get; }

        protected override DocumentReader CreateDocumentReader()
        {
            string key = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_KEY")!;
            string endpoint = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_ENDPOINT")!;

            AzureKeyCredential credential = new(key);
            DocumentIntelligenceClient client = new(new Uri(endpoint), credential);

            return new DocumentIntelligenceReader(client, ModelName);
        }
    }

    public sealed class DocumentIntelligenceReaderLayoutModel : DocumentIntelligenceReaderTests
    {
        /// <summary>
        /// https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview?view=doc-intel-4.0.0#layout
        /// </summary>
        protected override string ModelName => "prebuilt-layout";
    }

    public sealed class DocumentIntelligenceReaderReadModel : DocumentIntelligenceReaderTests
    {
        /// <summary>
        /// https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview?view=doc-intel-4.0.0#read
        /// </summary>
        protected override string ModelName => "prebuilt-read";
    }
}
