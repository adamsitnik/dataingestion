// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Enriches chunks with keyword extraction using an AI chat model.
/// </summary>
/// <remarks>
/// It adds "keywords" metadata to each chunk. It's an array of strings representing the extracted keywords.
/// </remarks>
public sealed class KeywordEnricher : IChunkProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;
    private readonly TextContent _request;

    // API design: predefinedKeywords needs to be provided in explicit way, so the user is encouraged to think about it.
    // And for example provide a closed set, so the results are more predictable.
    public KeywordEnricher(IChatClient chatClient, string[]? predefinedKeywords,
        ChatOptions? chatOptions = null, int maxKeywords = 5, double confidenceThreshold = 0.7)
    {
        if (confidenceThreshold < 0.0 || confidenceThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold), "The confidence threshold must be between 0.0 and 1.0.");
        }

        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
        _request = CreateLlmRequest(maxKeywords, predefinedKeywords, confidenceThreshold);
    }

    public static string MetadataKey => "keywords";

    public async Task<List<DocumentChunk>> ProcessAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        foreach (DocumentChunk chunk in chunks)
        {
            ChatResponse<string[]> response = await _chatClient.GetResponseAsync<string[]>(
            [
                new(ChatRole.User,
                [
                    _request,
                    new TextContent(chunk.Content),
                ])
            ], _chatOptions, cancellationToken: cancellationToken);

            chunk.Metadata[MetadataKey] = response.Result;
        }

        return chunks;
    }

    private static TextContent CreateLlmRequest(int maxKeywords, string[]? predefinedKeywords, double confidenceThreshold)
    {
        StringBuilder sb = new($"You are a keyword extraction expert. Analyze the given text and extract up to {maxKeywords} most relevant keywords.");

        if (predefinedKeywords is not null && predefinedKeywords.Length > 0)
        {
            sb.Append($" Focus on extracting keywords from the following predefined list: {string.Join(", ", predefinedKeywords)}.");
        }

        sb.Append($" Exclude keywords with confidence score below {confidenceThreshold}.");

        return new(sb.ToString());
    }
}
