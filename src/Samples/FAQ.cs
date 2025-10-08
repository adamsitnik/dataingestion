// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.VectorData;

namespace Samples;

public sealed class QAWriter : IngestionChunkWriter
{
    private readonly VectorStoreCollection<Guid, QARecord> _vectorStoreCollection;
    private readonly IChatClient _chatClient;

    public QAWriter(VectorStoreCollection<Guid, QARecord> vectorStoreCollection, IChatClient chatClient)
    {
        _vectorStoreCollection = vectorStoreCollection;
        _chatClient = chatClient;
    }

    protected override void Dispose(bool disposing)
    {
        _vectorStoreCollection.Dispose();
        _chatClient.Dispose();
    }

    public override async Task WriteAsync(IReadOnlyList<IngestionChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _vectorStoreCollection.EnsureCollectionExistsAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            ChatResponse<QA[]> chatResponse = await _chatClient.GetResponseAsync<QA[]>([
                new(ChatRole.User,
                [
                    new TextContent("Write a FAQ for this text. Make it no longer than 10 questions and answers."),
                    new TextContent(chunk.Context)
                ])
            ], cancellationToken: cancellationToken);

            await _vectorStoreCollection.UpsertAsync(
                chatResponse.Result.Select(r => new QARecord()
                {
                    Id = Guid.NewGuid(),
                    Question = r.Question,
                    Answer = r.Answer
                }), cancellationToken);
        }
    }
}

public class QA
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public sealed class QARecord
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData]
    public string Question { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536)]
    public string Embedding => Question;

    [VectorStoreData]
    public string Answer { get; set; } = string.Empty;
}
