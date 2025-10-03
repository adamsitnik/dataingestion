using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.DataIngestion.Tests;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;

namespace AspireSamples.Web.Services.Ingestion;

public class DataIngestor(
    ILoggerFactory loggerFactory,
    VectorStore vectorStore,
    IChatClient chatClient,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public async Task IngestDataAsync(DirectoryInfo directory, string searchPattern)
    {
        using VectorStoreWriter writer = new(vectorStore, dimensionCount: IngestedChunk.VectorDimensions, new()
        {
            CollectionName = IngestedChunk.CollectionName,
            DistanceFunction = IngestedChunk.VectorDistanceFunction,
            // Let's delete existing records for the same Document Id (by default it's the URI/file path), before inserting new ones.
            IncrementalIngestion = true
        });

        using DocumentPipeline pipeline = new(
            new MarkItDownReader(), // requires MarkItDown to be installed and in PATH
            [RemovalProcessor.Footers, RemovalProcessor.EmptySections],
            new SemanticChunker(embeddingGenerator, TiktokenTokenizer.CreateForModel("gpt-4o")),
            [], // [new SummaryEnricher(chatClient)], takes too much time for samples
            writer,
            loggerFactory);

        await pipeline.ProcessAsync(directory, searchPattern);
    }
}
