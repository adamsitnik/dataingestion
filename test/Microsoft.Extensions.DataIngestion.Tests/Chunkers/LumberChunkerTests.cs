// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

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

        [Fact]
        public async Task MultipleTopicsParagraphs()
        {
            IngestionDocument doc = new IngestionDocument("doc");

            // Topic 1
            string text1 = ".NET is a free, cross-platform, open-source developer platform for building many kinds of applications. It can run programs written in multiple languages, with C# being the most popular.";
            string text2 = "It relies on a high-performance runtime that is used in production by many high-scale apps.";

            // Topic 2
            string text3 = "Zeus is the chief deity of the Greek pantheon. He is a sky and thunder god in ancient Greek religion and mythology.";
            string text4 = "According to ancient myths, Zeus overthrew his father Cronus and became the ruler of Mount Olympus.";

            // Topic 3
            string text5 = "Quantum mechanics is a fundamental theory in physics that describes nature at the scale of atoms and subatomic particles.";
            string text6 = "The theory introduces concepts like wave-particle duality and the uncertainty principle discovered by Heisenberg.";

            // Topic 4
            string text7 = "French cuisine is renowned worldwide for its finesse and flavor. It has influenced Western cooking traditions for centuries.";
            string text8 = "Classic French techniques include sautéing, braising, and creating rich sauces like béchamel and hollandaise.";

            // Topic 5
            string text9 = "NASA's Artemis program aims to return humans to the Moon and establish a sustainable presence there.";
            string text10 = "The program will serve as a stepping stone for future crewed missions to Mars in the coming decades.";

            doc.Sections.Add(new IngestionDocumentSection
            {
                Elements =
                {
                    new IngestionDocumentParagraph(text1),
                    new IngestionDocumentParagraph(text2),
                    new IngestionDocumentParagraph(text3),
                    new IngestionDocumentParagraph(text4),
                    new IngestionDocumentParagraph(text5),
                    new IngestionDocumentParagraph(text6),
                    new IngestionDocumentParagraph(text7),
                    new IngestionDocumentParagraph(text8),
                    new IngestionDocumentParagraph(text9),
                    new IngestionDocumentParagraph(text10)
                }
            });

            IngestionChunker<string> chunker = CreateDocumentChunker();
            IReadOnlyList<IngestionChunk<string>> chunks = await chunker.ProcessAsync(doc).ToListAsync();

            Assert.Equal(5, chunks.Count);
            Assert.Equal(text1 + Environment.NewLine + text2, chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(text3 + Environment.NewLine + text4, chunks[1].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(text5 + Environment.NewLine + text6, chunks[2].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(text7 + Environment.NewLine + text8, chunks[3].Content, ignoreLineEndingDifferences: true);
            Assert.Equal(text9 + Environment.NewLine + text10, chunks[4].Content, ignoreLineEndingDifferences: true);
        }
    }
}
