// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

// This name is not final, we need to find a better one.
public sealed class AlternativeTextEnricher : IDocumentProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;

    public AlternativeTextEnricher(IChatClient chatClient, ChatOptions? chatOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
    }

    public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        foreach (DocumentImage image in document.OfType<DocumentImage>())
        {
            if (image.Content.HasValue && !string.IsNullOrEmpty(image.MediaType)
                && string.IsNullOrEmpty(image.AlternativeText))
            {
                var response = await _chatClient.GetResponseAsync(
                [
                    new(ChatRole.User,
                    [
                        new TextContent("Write a detailed alternative text for this image with less than 50 words."),
                        new DataContent(image.Content.Value, image.MediaType!),
                    ])
                ], _chatOptions, cancellationToken: cancellationToken);

                image.AlternativeText = response.Text;
            }
        }

        return document;
    }
}
