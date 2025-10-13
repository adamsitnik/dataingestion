// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using System;

namespace Microsoft.Extensions.DataIngestion.Processors.Tests;

public class ChatClientTestBase
{
    public ChatClientTestBase()
    {
        string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
        string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;

        AzureOpenAIClient openAIClient = new(new Uri(endpoint), new AzureKeyCredential(key));

        ChatClient = openAIClient.GetChatClient("gpt-4.1").AsIChatClient();
    }

    protected IChatClient ChatClient { get; }
}
