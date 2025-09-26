// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion.Chunkers;
using OpenAI;
using OpenAI.Embeddings;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class SemanticChunkerTests : DocumentChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker()
        {
            EmbeddingClient embeddingClient = CreateEmbeddingClient();
            return new SemanticChunker(embeddingClient.AsIEmbeddingGenerator());
        }

        private EmbeddingClient CreateEmbeddingClient()
        {
            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            return new EmbeddingClient(
                "text-embedding-3-small",
                credential: new ApiKeyCredential(key),
                options: new OpenAIClientOptions()
                {
                    Endpoint = new Uri(endpoint)
                }
            );
        }

        [Fact]
        public async Task SingleParagph()
        {
            string text = ".NET is a free, cross-platform, open-source developer platform for building many kinds of applications. It can run programs written in multiple languages, with C# being the most popular. It relies on a high-performance runtime that is used in production by many high-scale apps.";
            Document doc = new Document("doc");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph(text)
                }
            });
            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Single(chunks);
            Assert.Equal(text, chunks[0].Content);
        }

        [Fact]
        public async Task TwoTopicsParagraphs()
        {
            Document doc = new Document("doc");
            string text1 = ".NET is a free, cross-platform, open-source developer platform for building many kinds of applications. It can run programs written in multiple languages, with C# being the most popular.";
            string text2 = "It relies on a high-performance runtime that is used in production by many high-scale apps.";
            string text3 = "Zeus is the chief deity of the Greek pantheon. He is a sky and thunder god in ancient Greek religion and mythology.";
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentParagraph(text1),
                    new DocumentParagraph(text2),
                    new DocumentParagraph(text3)
                }
            });

            IDocumentChunker chunker = CreateDocumentChunker();
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            Assert.Equal(2, chunks.Count);
            Assert.Equal(String.Join(" ", text1, text2), chunks[0].Content);
            Assert.Equal(text3, chunks[1].Content);
        }
    }
}
