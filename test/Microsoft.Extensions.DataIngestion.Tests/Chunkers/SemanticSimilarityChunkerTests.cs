// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using OpenAI.Embeddings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class SemanticSimilarityChunkerTests : DocumentChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker(int maxTokensPerChunk = 2_000, int overlapTokens = 500)
        {
            EmbeddingClient embeddingClient = CreateEmbeddingClient();
            Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new SemanticSimilarityChunker(embeddingClient.AsIEmbeddingGenerator(), tokenizer,
                new() { MaxTokensPerChunk = maxTokensPerChunk, OverlapTokens = overlapTokens });
        }

        private EmbeddingClient CreateEmbeddingClient()
        {
            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            AzureOpenAIClient openAIClient = new(new Uri(endpoint), new AzureKeyCredential(key));

            return openAIClient.GetEmbeddingClient("text-embedding-3-small");
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
            Assert.Equal(text1 + Environment.NewLine + text2, chunks[0].Content);
            Assert.Equal(text3, chunks[1].Content);
        }

        [Fact]
        public async Task TwoSeparateTopicsWithAllKindsOfElements()
        {
            string dotNetTableMarkdown = @"| Language | Type | Status |
| --- | --- | --- |
| C# | Object-oriented | Primary |
| F# | Functional | Official |
| Visual Basic | Object-oriented | Official |
| PowerShell | Scripting | Supported |
| IronPython | Dynamic | Community |
| IronRuby | Dynamic | Community |
| Boo | Object-oriented | Community |
| Nemerle | Functional/OOP | Community |";

            string godsTableMarkdown = @"| God | Domain | Symbol | Roman Name |
| --- | --- | --- | --- |
| Zeus | Sky & Thunder | Lightning Bolt | Jupiter |
| Hera | Marriage & Family | Peacock | Juno |
| Poseidon | Sea & Earthquakes | Trident | Neptune |
| Athena | Wisdom & War | Owl | Minerva |
| Apollo | Sun & Music | Lyre | Apollo |
| Artemis | Hunt & Moon | Silver Bow | Diana |
| Aphrodite | Love & Beauty | Dove | Venus |
| Ares | War & Courage | Spear | Mars |
| Hephaestus | Fire & Forge | Hammer | Vulcan |
| Demeter | Harvest & Nature | Wheat | Ceres |
| Dionysus | Wine & Festivity | Grapes | Bacchus |
| Hermes | Messages & Trade | Caduceus | Mercury |";

            Document doc = new("dotnet-languages");
            doc.Sections.Add(new DocumentSection
            {
                Elements =
                {
                    new DocumentHeader("# .NET Supported Languages") { Level = 1 },
                    new DocumentParagraph("The .NET platform supports multiple programming languages:"),
                    new DocumentTable(dotNetTableMarkdown, CreateLanguageTableCells()),
                    new DocumentParagraph("C# remains the most popular language for .NET development."),
                    new DocumentHeader("# Ancient Greek Olympian Gods") { Level = 1 },
                    new DocumentParagraph("The twelve Olympian gods were the principal deities of the Greek pantheon:"),
                    new DocumentTable(godsTableMarkdown, CreateGreekGodsTableCells()),
                    new DocumentParagraph("These gods resided on Mount Olympus and ruled over different aspects of mortal and divine life.")
                }
            });

            IDocumentChunker chunker = CreateDocumentChunker(maxTokensPerChunk: 200, overlapTokens: 0);
            List<DocumentChunk> chunks = await chunker.ProcessAsync(doc);
            
            Assert.Equal(3, chunks.Count);
            Assert.All(chunks, chunk => Assert.Same(doc, chunk.Document));
            Assert.Equal($@"# .NET Supported Languages
The .NET platform supports multiple programming languages:
{dotNetTableMarkdown}
C# remains the most popular language for .NET development."
            , chunks[0].Content, ignoreLineEndingDifferences: true);
            Assert.Equal($@"# Ancient Greek Olympian Gods
The twelve Olympian gods were the principal deities of the Greek pantheon:
| God | Domain | Symbol | Roman Name |
| --- | --- | --- | --- |
| Zeus | Sky & Thunder | Lightning Bolt | Jupiter |
| Hera | Marriage & Family | Peacock | Juno |
| Poseidon | Sea & Earthquakes | Trident | Neptune |
| Athena | Wisdom & War | Owl | Minerva |
| Apollo | Sun & Music | Lyre | Apollo |
| Artemis | Hunt & Moon | Silver Bow | Diana |
| Aphrodite | Love & Beauty | Dove | Venus |
| Ares | War & Courage | Spear | Mars |
| Hephaestus | Fire & Forge | Hammer | Vulcan |
| Demeter | Harvest & Nature | Wheat | Ceres |
| Dionysus | Wine & Festivity | Grapes | Bacchus |"
            , chunks[1].Content, ignoreLineEndingDifferences: true);
            Assert.Equal($@"| God | Domain | Symbol | Roman Name |
| --- | --- | --- | --- |
| Hermes | Messages & Trade | Caduceus | Mercury |
These gods resided on Mount Olympus and ruled over different aspects of mortal and divine life."
            , chunks[2].Content, ignoreLineEndingDifferences: true);

            static string[,] CreateGreekGodsTableCells()
            {
                return new string[,]
                {
                    { "God", "Domain", "Symbol", "Roman Name" },
                    { "Zeus", "Sky & Thunder", "Lightning Bolt", "Jupiter" },
                    { "Hera", "Marriage & Family", "Peacock", "Juno" },
                    { "Poseidon", "Sea & Earthquakes", "Trident", "Neptune" },
                    { "Athena", "Wisdom & War", "Owl", "Minerva" },
                    { "Apollo", "Sun & Music", "Lyre", "Apollo" },
                    { "Artemis", "Hunt & Moon", "Silver Bow", "Diana" },
                    { "Aphrodite", "Love & Beauty", "Dove", "Venus" },
                    { "Ares", "War & Courage", "Spear", "Mars" },
                    { "Hephaestus", "Fire & Forge", "Hammer", "Vulcan" },
                    { "Demeter", "Harvest & Nature", "Wheat", "Ceres" },
                    { "Dionysus", "Wine & Festivity", "Grapes", "Bacchus" },
                    { "Hermes", "Messages & Trade", "Caduceus", "Mercury" }
                };
            }

            static string[,] CreateLanguageTableCells()
            {
                return new string[,]
                {
                    { "Language", "Type", "Status" },
                    { "C#", "Object-oriented", "Primary" },
                    { "F#", "Functional", "Official" },
                    { "Visual Basic", "Object-oriented", "Official" },
                    { "PowerShell", "Scripting", "Supported" },
                    { "IronPython", "Dynamic", "Community" },
                    { "IronRuby", "Dynamic", "Community" },
                    { "Boo", "Object-oriented", "Community" },
                    { "Nemerle", "Functional/OOP", "Community" }
                };
            }
        }
    }
}
