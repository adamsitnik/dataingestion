// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using LlamaParse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Tests;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.SqlServer;
using System.CommandLine;

namespace Samples
{
    internal class Program
    {
        internal const int DimensionCount = 1536; // text-embedding-3-small

        static Task<int> Main(string[] args)
        {
            Option<string> readerOption = new("--reader", "-r")
            {
                Description = "The document reader to use. Options are 'markitdown', 'markdown', 'adi' (Azure Document Intelligence), and 'llama' (LlamaParse).",
                Required = true,
            };
            readerOption.AcceptOnlyFromAmong("markitdown", "markdown", "adi", "llama");
            readerOption.Validators.Add(result =>
            {
                string? errorMessage = result.Tokens.Single().Value switch
                {
                    "llama" when string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LLAMACLOUD_API_KEY")) => "LLAMACLOUD_API_KEY environment variable is not set.",
                    "adi" when string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_KEY")) => "AZURE_DOCUMENT_INT_KEY environment variable is not set.",
                    "adi" when string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_ENDPOINT")) => "AZURE_DOCUMENT_INT_ENDPOINT environment variable is not set.",
                    _ => null
                };

                if (errorMessage is not null)
                {
                    result.AddError(errorMessage);
                }
            });
            Option<bool> extractImagesOption = new("--extract-images", "-e")
            {
                Description = "Whether to extract images (if supported by the selected reader).",
            };
            extractImagesOption.Validators.Add(result =>
            {
                if (!result.Implicit)
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")))
                    {
                        result.AddError("AZURE_OPENAI_ENDPOINT environment variable is not set.");
                    }
                    else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")))
                    {
                        result.AddError("AZURE_OPENAI_API_KEY environment variable is not set.");
                    }
                }
            });
            Option<FileInfo[]> filesOption = new("--files", "-f")
            {
                Description = "The files to process.",
            };
            filesOption.AcceptExistingOnly();
            Option<Uri[]> linksOptions = new("--links", "-l")
            {
                Description = "The URIs to process.",
                CustomParser = result => result.Tokens.Select(t => new Uri(t.Value)).ToArray()
            };
            RootCommand rootCommand = new("Data Ingestion Sample")
            {
                readerOption,
                extractImagesOption,
                filesOption,
                linksOptions
            };
            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                bool extractImages = parseResult.GetValue(extractImagesOption);
                string readerId = parseResult.GetRequiredValue(readerOption);

                await parseResult.InvocationConfiguration.Output.WriteLineAsync($"The selected reader is {readerId} with extract images set to {extractImages}.");

                FileInfo[]? files = parseResult.GetValue(filesOption);
                Uri[]? links = parseResult.GetValue(linksOptions);

                if ((files is null || files.Length == 0) && (links is null || links.Length == 0))
                {
                    await parseResult.InvocationConfiguration.Error.WriteLineAsync("No files or links specified to process. Use --files and/or --links options.");
                    return 1;
                }

                DocumentReader reader = CreateReader(readerId, extractImages);
                DocumentProcessor[] processors = CreateProcessors(extractImages);
                await parseResult.InvocationConfiguration.Output.WriteLineAsync($"The selected processors are: {string.Join<DocumentProcessor>(Environment.NewLine, processors)}.");

                DocumentChunker chunker = new HeaderChunker(
                    TiktokenTokenizer.CreateForModel("gpt-4"),
                    // Chunk size comes from https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-chunk-documents#text-split-skill-example
                    maxTokensPerParagraph: 2000,
                    overlapTokens: 500);

                using SqlServerVectorStore sqlServerVectorStore = new(
                    Environment.GetEnvironmentVariable("SQL_SERVER_CONNECTION_STRING")!,
                    new()
                    {
                        EmbeddingGenerator = CreateEmbeddingGenerator(),
                    });

                using SqlServerCollection<Guid, ChunkRecord> collection = sqlServerVectorStore.GetCollection<Guid, ChunkRecord>("chunks");
                List<Guid> ids = [];
                using DocumentWriter writer = new VectorStoreWriter<Guid, ChunkRecord>(collection, (doc, chunk) =>
                {
                    Guid recordId = Guid.NewGuid();
                    ids.Add(recordId);

                    return new()
                    {
                        Id = recordId,
                        Context = chunk.Context,
                        Content = chunk.Content,
                        Embedding = chunk.Content,
                        DocumentId = doc.Identifier
                    };
                });

                DocumentPipeline pipeline = new(reader, processors, chunker, writer);

                if (files?.Length > 0)
                {
                    await parseResult.InvocationConfiguration.Output.WriteLineAsync($"Processing {files.Length} files...");
                    await pipeline.ProcessAsync(files.Select(info => info.FullName), cancellationToken);
                }
                else
                {
                    await parseResult.InvocationConfiguration.Output.WriteLineAsync($"Processing {links!.Length} links...");
                    await pipeline.ProcessAsync(links, cancellationToken);
                }

                await parseResult.InvocationConfiguration.Output.WriteLineAsync($"Processed {ids.Count} chunks.");

                ChunkRecord[] retrieved = await collection.GetAsync(ids).ToArrayAsync();
                await parseResult.InvocationConfiguration.Output.WriteLineAsync($"Retrieved {retrieved.Length} chunks from the vector store.");

                return 0;
            });

            return rootCommand.Parse(args).InvokeAsync();
        }

        private static DocumentReader CreateReader(string readerId, bool extractImages)
            => readerId switch
            {
                "llama" => new LlamaParseReader(new LlamaParseClient(new HttpClient(),
                    new Configuration()
                    {
                        ApiKey = Environment.GetEnvironmentVariable("LLAMACLOUD_API_KEY")!,
                        ItemsToExtract = extractImages ? ItemType.Image | ItemType.Table : ItemType.Table
                    })),
                "markitdown" => new MarkItDownReader(),
                "markdown" => new MarkdownReader(),
                "adi" => new DocumentIntelligenceReader(
                    new DocumentIntelligenceClient(
                        new Uri(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_ENDPOINT")!),
                        new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INT_KEY")!)),
                    extractImages: extractImages),
                _ => throw new NotSupportedException($"The specified reader '{readerId}' is not supported.")
            };

        private static DocumentProcessor[] CreateProcessors(bool extractImages)
        {
            if (!extractImages)
            {
                return [];
            }

            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            AzureOpenAIClient openAIClient = new(new Uri(endpoint), new AzureKeyCredential(key));

            return [new ImageDescriptionProcessor(openAIClient.GetChatClient("gpt-4.1").AsIChatClient())];
        }

        private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator()
        {
            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            AzureOpenAIClient openAIClient = new(new Uri(endpoint), new AzureKeyCredential(key));

            return openAIClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();
        }
    }

    public class ChunkRecord
    {
        [VectorStoreKey(StorageName = "key")]
        public Guid Id { get; set; }

        [VectorStoreVector(Dimensions: Program.DimensionCount, StorageName = "embedding")]
        public string Embedding { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "content")]
        public string Content { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "context")]
        public string? Context { get; set; }

        [VectorStoreData(StorageName = "doc_id")]
        public string DocumentId { get; set; } = string.Empty;
    }
}
