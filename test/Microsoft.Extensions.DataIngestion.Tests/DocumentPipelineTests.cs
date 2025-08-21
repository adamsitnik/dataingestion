// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using LlamaParse;
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
    public static TheoryData<string[], DocumentReader> DocumentReaders
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

            TheoryData<string[], DocumentReader> theoryData = new();
            foreach (DocumentReader reader in documentReaders)
            {
                string[] filePaths = reader switch
                {
                    MarkdownReader => markdownFiles,
                    _ => nonMarkdownFiles
                };

                theoryData.Add(filePaths, reader);
            }

            return theoryData;
        }
    }

    [Theory]
    [MemberData(nameof(DocumentReaders))]
    public async Task CanProcessDocuments(string[] filePaths, DocumentReader reader)
    {
        DocumentProcessor[] documentProcessors = [new DocumentFlattener()];
        DocumentChunker documentChunker = new DummyChunker();
        List<Guid> ids = [];
        TestEmbeddingGenerator embeddingGenerator = new();
        InMemoryVectorStoreOptions options = new()
        {
            EmbeddingGenerator = embeddingGenerator
        };
        using InMemoryVectorStore testVectorStore = new(options);
        using InMemoryCollection<Guid, TestRecord> inMemoryCollection = testVectorStore.GetCollection<Guid, TestRecord>("testCollection");
        using VectorStoreWriter<Guid, TestRecord> vectorStoreWriter = new(inMemoryCollection, (doc, chunk) =>
        {
            Guid recordId = Guid.NewGuid();
            ids.Add(recordId);

            return new()
            {
                Id = recordId,
                Content = chunk.Content,
                DocumentId = doc.Identifier
            };
        });

        DocumentPipeline pipeline = new(reader, documentProcessors, documentChunker, vectorStoreWriter);
        await pipeline.ProcessAsync(filePaths);

        Assert.NotEmpty(ids);
        Assert.True(embeddingGenerator.WasCalled, "Embedding generator should have been called.");

        TestRecord[] retrieved = await inMemoryCollection.GetAsync(ids).ToArrayAsync();
        Assert.Equal(ids.Count, retrieved.Length);
        for (int i = 0; i < retrieved.Length; i++)
        {
            Assert.Equal(ids[i], retrieved[i].Id);
            Assert.NotEmpty(retrieved[i].Content);
            Assert.NotEmpty(retrieved[i].DocumentId);
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
}
