// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Azure;
using Azure.AI.DocumentIntelligence;
using System;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public class DocumentIntelligenceReaderTests : DocumentReaderConformanceTests
    {
        protected override DocumentReader CreateDocumentReader(bool extractImages = false)
        {
            string key = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_KEY")!;
            string endpoint = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_ENDPOINT")!;

            AzureKeyCredential credential = new(key);
            DocumentIntelligenceClient client = new(new Uri(endpoint), credential);

            return new DocumentIntelligenceReader(client);
        }
    }
}
