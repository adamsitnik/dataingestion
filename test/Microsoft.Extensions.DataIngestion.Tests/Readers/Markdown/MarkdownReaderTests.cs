// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Readers.Tests;

public class MarkdownReaderTests : DocumentReaderConformanceTests
{
    protected override IngestionDocumentReader CreateDocumentReader(bool extractImages = false) => new MarkdownReader();

    public static new IEnumerable<object[]> Sources
    {
        get
        {
            yield return new object[] { "https://raw.githubusercontent.com/microsoft/markitdown/main/README.md" };
        }
    }

    public static new IEnumerable<object[]> Files
    {
        get
        {
            yield return new object[] { Path.Combine("TestFiles", "Sample.md") };
        }
    }

    public static new IEnumerable<object[]> Images
    {
        get
        {
            yield return new object[] { Path.Combine("TestFiles", "SampleWithImage.md") };
        }
    }

    [Fact]
    public override Task SupportsTables() => SupportsTablesCore(Path.Combine("TestFiles", "Sample.md"));

    [Theory]
    [MemberData(nameof(Sources))]
    public override Task SupportsStreams(string uri) => base.SupportsStreams(uri);

    [Theory]
    [MemberData(nameof(Files))]
    public override Task SupportsFiles(string filePath) => base.SupportsFiles(filePath);

    [Theory]
    [MemberData(nameof(Images))]
    public override Task SupportsImages(string filePath) => base.SupportsImages(filePath);

    public override async Task SupportsTablesWithImages()
    {
        var table = await SupportsTablesWithImagesCore(Path.Combine("TestFiles", "TableWithImage.md"));

        for (int rowIndex = 1; rowIndex < table.Cells.GetLength(0); rowIndex++)
        {
            IngestionDocumentImage img = Assert.IsType<IngestionDocumentImage>(table.Cells[rowIndex, 1]);

            Assert.Equal("image/png", img.MediaType);
            Assert.NotNull(img.Content);
            Assert.False(img.Content.Value.IsEmpty);
        }
    }
}
