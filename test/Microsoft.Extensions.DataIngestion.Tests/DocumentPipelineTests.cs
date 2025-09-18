// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using LlamaParse;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DocumentPipelineTests
{
    public static TheoryData<string[], DocumentReader, DocumentChunker> FilesAndReaders
    {
        get
        {
            string[] nonMarkdownFiles =
            {
                Path.Combine("TestFiles", "Sample.pdf"),
                Path.Combine("TestFiles", "Sample.docx")
            };

            string[] markdownFiles =
            {
                Path.Combine("TestFiles", "Sample.md"),
            };

            List<DocumentReader> documentReaders = CreateReaders();
            List<DocumentChunker> documentChunkers = CreateChunkers();

            TheoryData<string[], DocumentReader, DocumentChunker> theoryData = new();
            foreach (DocumentReader reader in documentReaders)
            {
                string[] filePaths = reader switch
                {
                    MarkdownReader => markdownFiles,
                    _ => nonMarkdownFiles
                };

                foreach (DocumentChunker chunker in documentChunkers)
                {
                    theoryData.Add(filePaths, reader, chunker);
                }
            }

            return theoryData;
        }
    }

    [Theory]
    [MemberData(nameof(FilesAndReaders))]
    public async Task CanProcessDocuments(string[] filePaths, DocumentReader reader, DocumentChunker chunker)
    {
        DocumentProcessor[] documentProcessors = [new DocumentFlattener()];
        TestEmbeddingGenerator embeddingGenerator = new();
        InMemoryVectorStoreOptions options = new()
        {
            EmbeddingGenerator = embeddingGenerator
        };
        using InMemoryVectorStore testVectorStore = new(options);
        using VectorStoreWriter vectorStoreWriter = new(testVectorStore, dimensionCount: TestEmbeddingGenerator.DimensionCount);

        DocumentPipeline pipeline = new(reader, documentProcessors, chunker, [], vectorStoreWriter);
        await pipeline.ProcessAsync(filePaths);

        Assert.True(embeddingGenerator.WasCalled, "Embedding generator should have been called.");

        Dictionary<string, object?>[] retrieved = await vectorStoreWriter.VectorStoreCollection
            .GetAsync(record => filePaths.Contains(record["documentid"]), top: 1000)
            .ToArrayAsync();

        Assert.NotEmpty(retrieved);
        for (int i = 0; i < retrieved.Length; i++)
        {
            Assert.NotEmpty((string)retrieved[i]["key"]!);
            Assert.NotEmpty((string)retrieved[i]["content"]!);
            Assert.Contains((string)retrieved[i]["documentid"]!, filePaths);
        }
    }

    public static TheoryData<DocumentReader> Readers => new(CreateReaders());

    [Theory]
    [MemberData(nameof(Readers))]
    public async Task CanProcessDocumentsInDirectory(DocumentReader reader)
    {
        DocumentChunker documentChunker = new DummyChunker();
        TestEmbeddingGenerator embeddingGenerator = new();
        InMemoryVectorStoreOptions options = new()
        {
            EmbeddingGenerator = embeddingGenerator
        };
        using InMemoryVectorStore testVectorStore = new(options);
        using VectorStoreWriter vectorStoreWriter = new(testVectorStore, dimensionCount: TestEmbeddingGenerator.DimensionCount);

        DocumentPipeline pipeline = new(reader, [], documentChunker, [], vectorStoreWriter);

        DirectoryInfo directory = new("TestFiles");
        string searchPattern = reader switch
        {
            MarkdownReader => "*.md",
            _ => "*.docx"
        };
        await pipeline.ProcessAsync(directory, searchPattern);

        Assert.True(embeddingGenerator.WasCalled, "Embedding generator should have been called.");

        Dictionary<string, object?>[] retrieved = await vectorStoreWriter.VectorStoreCollection
            .GetAsync(record => ((string)record["documentid"]!).StartsWith(directory.FullName), top: 1000)
            .ToArrayAsync();

        Assert.NotEmpty(retrieved);
        for (int i = 0; i < retrieved.Length; i++)
        {
            Assert.NotEmpty((string)retrieved[i]["key"]!);
            Assert.NotEmpty((string)retrieved[i]["content"]!);
            Assert.StartsWith(directory.FullName, (string)retrieved[i]["documentid"]!);
        }
    }

    private static List<DocumentReader> CreateReaders()
    {
        List<DocumentReader> readers = new()
        {
            new MarkdownReader(),
            new MarkItDownReader(),
        };

        if (Environment.GetEnvironmentVariable("LLAMACLOUD_API_KEY") is string llamaKey && !string.IsNullOrEmpty(llamaKey))
        {
            LlamaParse.Configuration configuration = new()
            {
                ApiKey = llamaKey,
                ItemsToExtract = ItemType.Table,
            };

            readers.Add(new LlamaParseReader(new LlamaParseClient(new HttpClient(), configuration)));
        }

        if (Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_KEY") is string adiKey && !string.IsNullOrEmpty(adiKey)
            && Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_ENDPOINT") is string endpoint && !string.IsNullOrEmpty(endpoint))
        {
            AzureKeyCredential credential = new(adiKey);
            DocumentIntelligenceClient client = new(new Uri(endpoint), credential);

            readers.Add(new DocumentIntelligenceReader(client));
        }

        return readers;
    }

    private static List<DocumentChunker> CreateChunkers() => [
        new DummyChunker(),
        // Chunk size comes from https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-chunk-documents#text-split-skill-example
        new HeaderChunker(TiktokenTokenizer.CreateForModel("gpt-4"), 2000, 500)
    ];
}
