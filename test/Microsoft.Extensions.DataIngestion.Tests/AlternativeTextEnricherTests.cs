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
        ImageAlternativeTextEnricher sut = new(ChatClient);
        string imagePath = Path.Combine("TestFiles", "SampleImage.png");
        ReadOnlyMemory<byte> imageContent = await File.ReadAllBytesAsync(imagePath);

        IngestionDocumentImage documentImage = new($"![]({imagePath})")
        {
            AlternativeText = null,
            Content = imageContent,
            MediaType = "image/png"
        };

        IngestionDocumentImage tableCell = new($"![]({imagePath})")
        {
            AlternativeText = null,
            Content = imageContent,
            MediaType = "image/png"
        };

        IngestionDocument document = new("withImage")
        {
            Sections =
            {
                new IngestionDocumentSection
                {
                    Elements =
                    {
                        documentImage,
                        new IngestionDocumentTable("nvm", new[,] { { tableCell } })
                    }
                }
            }
        };

        await sut.ProcessAsync(document);

        Assert.NotNull(documentImage.AlternativeText);
        Assert.NotEmpty(documentImage.AlternativeText);

        Assert.NotNull(tableCell.AlternativeText);
        Assert.NotEmpty(tableCell.AlternativeText);
    }
}
