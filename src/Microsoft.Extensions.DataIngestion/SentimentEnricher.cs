// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Enriches chunks with sentiment analysis using an AI chat model.
/// </summary>
/// <remarks>
/// It adds the following metadata to each chunk:
/// <para>"Sentiment": The sentiment of the chunk (Positive, Negative, Neutral).</para>
/// <para>"SentimentConfidenceScore": A confidence score (0.0-1.0) indicating the certainty of the sentiment analysis.</para>
/// </remarks>
public sealed class SentimentEnricher : ChunkProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;

    public SentimentEnricher(IChatClient chatClient, ChatOptions? chatOptions = null)
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

        foreach (DocumentChunk chunk in chunks)
        {
            ChatResponse<SentimentWithScore> response = await _chatClient.GetResponseAsync<SentimentWithScore>(
            [
                new(ChatRole.User,
                [
                    new TextContent("You are a sentiment analysis expert. Analyze the sentiment of the given text and return a response with sentiment (Positive/Negative/Neutral) and confidence score (0.0-1.0)."),
                    new TextContent(chunk.Content),
                ])
            ], _chatOptions, cancellationToken: cancellationToken);

            chunk.Metadata[nameof(SentimentWithScore.Sentiment)] = response.Result.Sentiment;
            chunk.Metadata[nameof(SentimentWithScore.SentimentConfidenceScore)] = response.Result.SentimentConfidenceScore;
        }

        return chunks;
    }

    private class SentimentWithScore
    {
        public string Sentiment { get; set; } = string.Empty;
        // This name contains "Sentiment" prefix to avoid collisions with other "ConfidenceScore" keys.
        public double SentimentConfidenceScore { get; set; }
    }
}
