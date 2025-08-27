// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using LlamaParse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.SqlServer;
using System.CommandLine;

namespace Samples
{
    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            RootCommand rootCommand = CreateRootCommand();

            return rootCommand.Parse(args).InvokeAsync();
        }

        private static async Task<int> ProcessAsync(string readerId, bool extractImages, LogLevel logLevel,
            FileInfo[]? files, Uri[]? links, string? searchValue, CancellationToken cancellationToken)
        {
            using ILoggerFactory loggerFactory = CreateLoggerFactory(logLevel);

            DocumentReader reader = CreateReader(readerId, extractImages);
            DocumentProcessor[] processors = CreateProcessors(extractImages);

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

            using ChunkRecordVectorStoreWriter<Guid> writer = new(sqlServerVectorStore, 1536  /* text-embedding-3-small */);

            DocumentPipeline pipeline = new(reader, processors, chunker, writer, loggerFactory);

            if (files?.Length > 0)
            {
                await pipeline.ProcessAsync(files.Select(info => info.FullName), cancellationToken);
            }

            if (links?.Length > 0)
            {
                await pipeline.ProcessAsync(links!, cancellationToken);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                await foreach (var result in writer.VectorStoreCollection.SearchAsync(searchValue, top: 1))
                {
                    Console.WriteLine($"Score: {result.Score}\nContent: {result.Record.Content}\n");
                }
            }

            return 0;
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

        #region boilerplate
        private static ILoggerFactory CreateLoggerFactory(LogLevel logLevel)
            => LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(logLevel);

                builder.AddSimpleConsole(configure =>
                {
                    configure.TimestampFormat = "[HH:mm:ss] ";
                    configure.UseUtcTimestamp = true;
                });
            });

        private static RootCommand CreateRootCommand()
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
            Option<FileInfo[]> filesOption = new("--files", "-f")
            {
                Description = "The files to process.",
                AllowMultipleArgumentsPerToken = true,
            };
            filesOption.AcceptExistingOnly();
            Option<Uri[]> linksOptions = new("--links", "-l")
            {
                Description = "The URIs to process.",
                AllowMultipleArgumentsPerToken = true,
                CustomParser = result => result.Tokens.Select(t => new Uri(t.Value)).ToArray()
            };
            Option<LogLevel> logLevelOption = new("--log-level")
            {
                Description = "The minimum log level to use. Default is Information.",
                DefaultValueFactory = _ => LogLevel.Information
            };
            logLevelOption.AcceptOnlyFromAmong(Enum.GetNames(typeof(LogLevel)));
            Option<string> searchValue = new("--search")
            {
                Description = "The search value to use."
            };
            RootCommand rootCommand = new("Data Ingestion Sample")
            {
                readerOption,
                extractImagesOption,
                filesOption,
                linksOptions,
                logLevelOption,
                searchValue
            };
            rootCommand.Validators.Add(result =>
            {
                if (result.GetResult(filesOption) is null && result.GetResult(linksOptions) is null)
                {
                    result.AddError("At least one of --files or --links options must be specified.");
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")))
                {
                    result.AddError("AZURE_OPENAI_ENDPOINT environment variable is not set.");
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")))
                {
                    result.AddError("AZURE_OPENAI_API_KEY environment variable is not set.");
                }
            });

            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                bool extractImages = parseResult.GetValue(extractImagesOption);
                string readerId = parseResult.GetRequiredValue(readerOption);
                LogLevel logLevel = parseResult.GetValue(logLevelOption);

                FileInfo[]? files = parseResult.GetValue(filesOption);
                Uri[]? links = parseResult.GetValue(linksOptions);

                string? search = parseResult.GetValue(searchValue);

                return ProcessAsync(readerId, extractImages, logLevel, files, links, search, cancellationToken);
            });

            return rootCommand;
        }
#endregion boilerplate
    }
}
