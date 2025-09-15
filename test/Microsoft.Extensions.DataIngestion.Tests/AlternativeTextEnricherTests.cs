// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class AlternativeTextEnricherTests
{
    private readonly IChatClient _chatClient;

    public AlternativeTextEnricherTests()
    {
        string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
        string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

        AzureOpenAIClient openAIClient = new(new Uri(endpoint), new AzureKeyCredential(key));

        _chatClient = openAIClient.GetChatClient("gpt-4.1").AsIChatClient();
    }

    [Fact]
    public async Task CanGenerateImageAltText()
    {
        AlternativeTextEnricher sut = new(_chatClient);
        ReadOnlyMemory<byte> imageContent = await File.ReadAllBytesAsync(Path.Combine("TestFiles", "SampleImage.png"));

        DocumentImage documentImage = new()
        {
            AlternativeText = null,
            Content = BinaryData.FromBytes(imageContent),
            MediaType = "image/png"
        };

        Document document = new("withImage")
        {
            Sections =
            {
                new DocumentSection
                {
                    Elements =
                    {
                        documentImage
                    }
                }
            }
        };

        await sut.ProcessAsync(document);

        Assert.NotNull(documentImage.AlternativeText);
        Assert.NotEmpty(documentImage.AlternativeText);
    }
}
