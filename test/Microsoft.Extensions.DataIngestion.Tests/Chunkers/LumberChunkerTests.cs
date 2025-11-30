// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using System;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class LumberChunkerTests : BasicSemanticTests
    {
        protected override IngestionChunker<string> CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

            Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
            IngestionChunkerOptions options = new(tokenizer) { MaxTokensPerChunk = maxTokensPerChunk, OverlapTokens = overlapTokens };
            return new LumberChunker(options, client.GetChatClient("gpt-4.1").AsIChatClient());
        }
    }
}
