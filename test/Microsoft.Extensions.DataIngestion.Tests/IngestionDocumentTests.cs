// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class IngestionDocumentTests
{
    private readonly string[,] rows = { { "header" }, { "row1" }, { "row2" } };

    [Fact]
    public void EnumeratorFlattensTheStructureAndPreservesOrder()
    {
        IngestionDocument doc = new("withSubSections");
        doc.Sections.Add(new IngestionDocumentSection("first section")
        {
            Elements =
            {
                new IngestionDocumentHeader("header"),
                new IngestionDocumentParagraph("paragraph"),
                new IngestionDocumentTable("table", rows),
                new IngestionDocumentSection("nested section")
                {
                    Elements =
                    {
                        new IngestionDocumentHeader("nested header"),
                        new IngestionDocumentParagraph("nested paragraph")
                    }
                }
            }
        });
        doc.Sections.Add(new IngestionDocumentSection("second section")
        {
            Elements =
            {
                new IngestionDocumentHeader("header 2"),
                new IngestionDocumentParagraph("paragraph 2")
            }
        });

        IngestionDocumentElement[] flatElements = doc.ToArray();

        Assert.IsType<IngestionDocumentHeader>(flatElements[0]);
        Assert.Equal("header", flatElements[0].Markdown);
        Assert.IsType<IngestionDocumentParagraph>(flatElements[1]);
        Assert.Equal("paragraph", flatElements[1].Markdown);
        Assert.IsType<IngestionDocumentTable>(flatElements[2]);
        Assert.Equal("table", flatElements[2].Markdown);
        Assert.IsType<IngestionDocumentHeader>(flatElements[3]);
        Assert.Equal("nested header", flatElements[3].Markdown);
        Assert.IsType<IngestionDocumentParagraph>(flatElements[4]);
        Assert.Equal("nested paragraph", flatElements[4].Markdown);
        Assert.IsType<IngestionDocumentHeader>(flatElements[5]);
        Assert.Equal("header 2", flatElements[5].Markdown);
        Assert.IsType<IngestionDocumentParagraph>(flatElements[6]);
        Assert.Equal("paragraph 2", flatElements[6].Markdown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyParagraphDocumentCantBeCreated(string? input)
        => Assert.Throws<ArgumentNullException>(() => new IngestionDocumentParagraph(input!));
}
