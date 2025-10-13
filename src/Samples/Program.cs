// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using LlamaParse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
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
            FileInfo[] files, string? searchValue, CancellationToken cancellationToken)
        {
            using ILoggerFactory loggerFactory = CreateLoggerFactory(logLevel);

            IngestionDocumentReader reader = CreateReader(readerId, extractImages);
            List<IngestionDocumentProcessor> processors = CreateDocumentProcessors(extractImages);
            IngestionChunkProcessor<string>[] chunkProcessors = CreateChunkProcessors();

            IngestionChunker<string> chunker = new HeaderChunker(TiktokenTokenizer.CreateForModel("gpt-4"));

            using SqlServerVectorStore sqlServerVectorStore = new(
                Environment.GetEnvironmentVariable("SQL_SERVER_CONNECTION_STRING")!,
                new()
                {
                    EmbeddingGenerator = CreateEmbeddingGenerator(),
                });
            using VectorStoreWriter<string> writer = new(sqlServerVectorStore, 1536 /* text-embedding-3-small */);

            using DocumentPipeline<string> pipeline = new(reader, processors, chunker, chunkProcessors, writer, loggerFactory);

            await pipeline.ProcessAsync(files, cancellationToken);

            if (!string.IsNullOrEmpty(searchValue))
            {
                await foreach (var result in writer.VectorStoreCollection.SearchAsync(searchValue, top: 1))
                {
                    Console.WriteLine($"Score: {result.Score}\nContent: {result.Record["content"]}\n");
                }
            }

            return 0;
        }

        private static async Task<int> FAQAsync(string readerId, LogLevel logLevel,
            FileInfo[] files, CancellationToken cancellationToken)
        {
            using ILoggerFactory loggerFactory = CreateLoggerFactory(logLevel);

            IngestionDocumentReader reader = CreateReader(readerId, extractImages: false);
            List<IngestionDocumentProcessor> processors = CreateDocumentProcessors(extractImages: false);

            IngestionChunker<string> chunker = new HeaderChunker(TiktokenTokenizer.CreateForModel("gpt-4"));

            using SqlServerVectorStore sqlServerVectorStore = new(
                Environment.GetEnvironmentVariable("SQL_SERVER_CONNECTION_STRING")!,
                new()
                {
                    EmbeddingGenerator = CreateEmbeddingGenerator(),
                });
            using SqlServerCollection<Guid, QARecord> collection = sqlServerVectorStore.GetCollection<Guid, QARecord>("faq");

            AzureOpenAIClient openAIClient = CreateOpenAiClient();

            using QAWriter writer = new(collection, openAIClient.GetChatClient("gpt-4.1").AsIChatClient());

            using DocumentPipeline<string> pipeline = new(reader, processors, chunker, [], writer, loggerFactory);

            await pipeline.ProcessAsync(files, cancellationToken);

            while (true)
            {
                Console.Write("Enter your question (or 'exit' to quit): ");
                string? searchValue = Console.ReadLine();
                if (string.IsNullOrEmpty(searchValue) || searchValue.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                Console.WriteLine("Searching...\n");
                // For FAQ, we want to return multiple results
                await foreach (var result in collection.SearchAsync(searchValue, top: 3))
                {
                    Console.WriteLine($"Score: {result.Score}\n\tQuestion: {result.Record.Question}\n\tAnswer: {result.Record.Answer}\n");
                }
            }

            return 0;
        }

        private static IngestionDocumentReader CreateReader(string readerId, bool extractImages)
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

        private static List<IngestionDocumentProcessor> CreateDocumentProcessors(bool extractImages)
        {
            List<IngestionDocumentProcessor> processors = [RemovalProcessor.Footers, RemovalProcessor.EmptySections];

            if (extractImages)
            {
                AzureOpenAIClient openAIClient = CreateOpenAiClient();
                processors.Add(new ImageAlternativeTextEnricher(openAIClient.GetChatClient("gpt-4.1").AsIChatClient()));
            }

            return processors;
        }

        private static IngestionChunkProcessor<string>[] CreateChunkProcessors()
        {
            AzureOpenAIClient openAIClient = CreateOpenAiClient();
            return [new SummaryEnricher(openAIClient.GetChatClient("gpt-4.1").AsIChatClient())];
        }

        private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator()
        {
            AzureOpenAIClient openAIClient = CreateOpenAiClient();
            return openAIClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();
        }

        private static AzureOpenAIClient CreateOpenAiClient()
        {
            string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

            return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
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
                Arity = ArgumentArity.OneOrMore
            };
            filesOption.AcceptExistingOnly();
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

            Command faqCommand = new("faq", "Runs the FAQ sample.")
            {
                readerOption,
                filesOption,
                logLevelOption,
            };
            faqCommand.SetAction((parseResult, cancellationToken) =>
            {
                string readerId = parseResult.GetRequiredValue(readerOption);
                LogLevel logLevel = parseResult.GetValue(logLevelOption);
                FileInfo[] files = parseResult.GetValue(filesOption)!;
                return FAQAsync(readerId, logLevel, files, cancellationToken);
            });

            RootCommand rootCommand = new("Data Ingestion Sample")
            {
                readerOption,
                extractImagesOption,
                filesOption,
                logLevelOption,
                searchValue,
                faqCommand
            };

            rootCommand.Validators.Add(result =>
            {
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

                FileInfo[] files = parseResult.GetValue(filesOption)!;

                string? search = parseResult.GetValue(searchValue);

                return ProcessAsync(readerId, extractImages, logLevel, files, search, cancellationToken);
            });

            return rootCommand;
        }
        #endregion boilerplate
    }
}
