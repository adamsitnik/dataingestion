// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class PiiRemovalProcessorTests : ChatClientTestBase
{
    [Fact]
    public async Task CanRemovePii()
    {
        PiiRemovalProcessor sut = new(ChatClient);
        List<DocumentChunk> chunks = new()
        {
            new("My name is John Fakename and my email is john.fakename@microsft.com")
            {
                Metadata = { { "MakeSure", "MetadataIsPreserved" } },
            },
            new("These are not the droids you are looking for.")
        };

        List<DocumentChunk> result = await sut.ProcessAsync(chunks);

        Assert.Equal(2, result.Count);

        Assert.NotEqual(chunks[0].Content, result[0].Content);
        Assert.DoesNotContain("John Fakename", result[0].Content);
        Assert.DoesNotContain("john.fakename@microsft.com", result[0].Content);
        Assert.Null(result[0].TokenCount);
        Assert.Equal("MetadataIsPreserved", result[0].Metadata["MakeSure"]);

        Assert.Equal(chunks[1].Content, result[1].Content);
        Assert.Equal(chunks[1].TokenCount, result[1].TokenCount);
    }
}
