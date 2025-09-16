// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class SummaryEnricherTests : ChatClientTestBase
{
    [Fact]
    public async Task CanProvideSummary()
    {
        SummaryEnricher sut = new(ChatClient);

        List<DocumentChunk> chunks = new()
        {
            new DocumentChunk("I love programming! It's so much fun and rewarding."),
            new DocumentChunk("I hate bugs. They are so frustrating and time-consuming.")
        };

        await sut.ProcessAsync(chunks);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.NotEmpty((string)chunk.Metadata["Summary"]!));
    }
}
