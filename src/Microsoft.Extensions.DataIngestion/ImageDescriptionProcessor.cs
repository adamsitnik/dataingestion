// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

// This name is not final, we need to find a better one.
public sealed class ImageDescriptionProcessor : DocumentProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions? _chatOptions;

    public ImageDescriptionProcessor(IChatClient chatClient, ChatOptions? chatOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _chatOptions = chatOptions;
    }

    public override async ValueTask<List<Document>> ProcessAsync(List<Document> documents, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (documents is null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        foreach (DocumentImage image in GetImages(documents))
        {
            if (image.Content is not null && !string.IsNullOrEmpty(image.MediaType))
            {
                var response = await _chatClient.GetResponseAsync(
                [
                    new(ChatRole.User,
                    [
                        new TextContent("Write a detailed description for this image with less than 50 words."),
                        new DataContent(image.Content.ToMemory(), image.MediaType!),
                    ])
                ], _chatOptions, cancellationToken: cancellationToken);

                image.Description = response.Text;
            }
        }

        return documents;
    }

    private IEnumerable<DocumentImage> GetImages(List<Document> documents)
    {
        // For this particular processor the order does not matter, but since we already
        // use Stack<T> in other places, we will use it here as well.
        Stack<DocumentSection> sectionsToProcess = new();
        foreach (Document document in documents)
        {
            for (int sectionIndex = document.Sections.Count - 1; sectionIndex >= 0; sectionIndex--)
            {
                sectionsToProcess.Push(document.Sections[sectionIndex]);
            }

            while (sectionsToProcess.Count > 0)
            {
                DocumentSection currentSection = sectionsToProcess.Pop();

                for (int elementIndex = currentSection.Elements.Count - 1; elementIndex >= 0; elementIndex--)
                {
                    DocumentElement currentElement = currentSection.Elements[elementIndex];

                    if (currentElement is DocumentSection nestedSection)
                    {
                        sectionsToProcess.Push(nestedSection);
                    }
                    else if (currentElement is DocumentImage image)
                    {
                        yield return image;
                    }
                }
            }
        }
    }
}
