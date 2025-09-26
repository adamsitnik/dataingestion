using AspireSamples.Web.Services.Ingestion;
using Microsoft.Extensions.VectorData;

namespace AspireSamples.Web.Services;

public class SemanticSearch(
    VectorStoreCollection<Guid, IngestedChunk> vectorCollection,
    [FromKeyedServices("ingestion_directory")] DirectoryInfo ingestionDirectory,
    DataIngestor dataIngestor)
{
    private bool _initialized = false;

    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(string text, string? documentIdFilter, int maxResults)
    {
        if (!_initialized)
        {
            // Ensure the data is ingested before performing any searches.
            // We do it now in order to get nice tracing visualization.
            await dataIngestor.IngestDataAsync(ingestionDirectory, searchPattern: "*.pdf");

            _initialized = true;
        }

        var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
        {
            Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
        });

        return await nearest.Select(result => result.Record).ToListAsync();
    }
}
