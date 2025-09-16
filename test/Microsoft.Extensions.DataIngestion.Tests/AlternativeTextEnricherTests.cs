// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class AlternativeTextEnricherTests : ChatClientTestBase
{
    [Fact]
    public async Task CanGenerateImageAltText()
    {
        AlternativeTextEnricher sut = new(ChatClient);
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
