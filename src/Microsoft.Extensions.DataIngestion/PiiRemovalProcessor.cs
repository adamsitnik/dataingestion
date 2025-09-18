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
// Design note: it's IChunkProcessor, rather than DocumentProcessor, because if we were dealing with Document,
// we would need to update not just the Markdown of every DocumentElement, but also Text.
// And the Markdown of entire Document itself. Which could exceed the token limit of the AI model.
// Moreover, there are fewer chunks than elements, so processing chunks is more efficient.
// And when processing chunks, the LLM gets more context, which helps with identifying PII.
// The disadvantage of this approach is that this needs to be the first processor in the pipeline, otherwise the PII could become part of the Metadata of the chunk (for example: the Summary), which we do not process here.
public sealed class PiiRemovalProcessor : IChunkProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;

    public PiiRemovalProcessor(IChatClient chatClient, ChatOptions? chatOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
    }

    public async Task<List<DocumentChunk>> ProcessAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        List<DocumentChunk> result = new(chunks.Count);
        foreach (DocumentChunk chunk in chunks)
        {
            if (chunk.Metadata.Count > 0)
            {
                throw new InvalidOperationException("PiiRemovalProcessor does not support chunks with metadata. It should be the first processor in the pipeline, so it ensures that PII is removed before further processing.");
            }

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
