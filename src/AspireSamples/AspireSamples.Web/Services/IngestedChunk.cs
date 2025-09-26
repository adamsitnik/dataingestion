using Microsoft.Extensions.VectorData;

namespace AspireSamples.Web.Services;

public class IngestedChunk
{
    public const int VectorDimensions = 384; // 384 is the default vector size for the all-minilm embedding model
    public const string VectorDistanceFunction = DistanceFunction.CosineSimilarity;
    public const string CollectionName = "aspiresamples-chunks";

    [VectorStoreKey(StorageName = "key")]
    public required Guid Key { get; set; }

    [VectorStoreData(StorageName = "documentid")]
    public required string DocumentId { get; set; }

    [VectorStoreData(StorageName = "content")]
    public required string Text { get; set; }

    [VectorStoreData(StorageName = "context")]
    public string? Context { get; set; }

    [VectorStoreVector(VectorDimensions, DistanceFunction = VectorDistanceFunction, StorageName = "embedding")]
    public string? Vector => Text;
}
