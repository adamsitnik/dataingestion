// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class SentimentEnricherTests : ChatClientTestBase
{
    [Fact]
    public async Task CanProvideSentiment()
    {
        SentimentEnricher sut = new(ChatClient);

        List<DocumentChunk> chunks = new()
        {
            new("I love programming! It's so much fun and rewarding."),
            new("I hate bugs. They are so frustrating and time-consuming."),
            new("The weather is okay, not too bad but not great either."),
            new("I hate you. I am sorry, I actually don't. I am not sure myself what my feelings are.")
        };

        await sut.ProcessAsync(chunks);

        Assert.Equal(4, chunks.Count);

        Assert.Equal("Positive", chunks[0].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Negative", chunks[1].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Neutral", chunks[2].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Unknown", chunks[3].Metadata[SentimentEnricher.MetadataKey]);
    }
}
