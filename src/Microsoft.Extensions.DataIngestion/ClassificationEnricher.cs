// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Enriches document chunks with a classification label based on their content.
/// </summary>
/// <remarks>This class uses a chat-based language model to analyze the content of document chunks and assign a
/// single, most relevant classification label. The classification is performed using a predefined set of classes, with
/// an optional fallback class for cases where no suitable classification can be determined.</remarks>
public class ClassificationEnricher : ChunkProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;
    private readonly TextContent _request;

    public ClassificationEnricher(IChatClient chatClient, string[] predefinedClasses,
        ChatOptions? chatOptions = null, string fallbackClass = "Unknown")
    {
        if (predefinedClasses is null || predefinedClasses.Length == 0)
        {
            throw new ArgumentException("Predefined classes must be provided.", nameof(predefinedClasses));
        }
        else if (string.IsNullOrEmpty(fallbackClass))
        {
            throw new ArgumentException("Fallback class must be provided.", nameof(fallbackClass));
        }

        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
        _request = CreateLlmRequest(predefinedClasses, fallbackClass);
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
            var response = await _chatClient.GetResponseAsync(
            [
                new(ChatRole.User,
                [
                    _request,
                    new TextContent(chunk.Content),
                ])
            ], _chatOptions, cancellationToken: cancellationToken);

            chunk.Metadata["Classification"] = response.Text;
        }

        return chunks;
    }

    private static TextContent CreateLlmRequest(string[] predefinedClasses, string fallbackClass)
        => new($"You are a classification expert. Analyze the given text and assign single, most relevant class. " +
            $"Use only the following predefined classes: {string.Join(", ", predefinedClasses)} and return {fallbackClass} when unable to classify.");
}
