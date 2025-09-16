// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// This processor removes Personally Identifiable Information (PII) from document chunks using an AI chat model.
/// </summary>
/// design note: It could be a DocumentProcessor as well, with accurate token count, but at a cost of some tradeoffs like complexity and performance.
public sealed class PiiRemovalProcessor : ChunkProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;

    public PiiRemovalProcessor(IChatClient chatClient, ChatOptions? chatOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
    }

    public override async Task<List<DocumentChunk>> ProcessAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        List<DocumentChunk> result = new(chunks.Count);
        foreach (DocumentChunk chunk in chunks)
        {
            var response = await _chatClient.GetResponseAsync(
            [
                new(ChatRole.User,
                [
                    new TextContent($"You are a data privacy expert. Analyze given text and remove all Personally Identifiable Information. Return just the updated text or nothing when no changes were needed."),
                    new TextContent(chunk.Content),
                ])
            ], _chatOptions, cancellationToken: cancellationToken);

            if (string.IsNullOrEmpty(response.Text))
            {
                result.Add(chunk);
                continue;
            }

            // The token count is unknown at this point.
            DocumentChunk updated = new(response.Text, tokenCount: null, chunk.Context);
            foreach (var kvp in chunk.Metadata)
            {
                updated.Metadata[kvp.Key] = kvp.Value;
            }

            result.Add(updated);
        }

        return result;
    }
}
