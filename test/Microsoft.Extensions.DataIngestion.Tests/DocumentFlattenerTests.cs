// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DocumentFlattenerTests
{
    [Fact]
    public async Task StructureIsPreservedWithinSectionsAndDocument()
    {
        Document doc = new("1")
        {
            Markdown = "same",
        };
        doc.Sections.Add(new DocumentSection
        {
            Markdown = "first section",
            Elements =
            {
                new DocumentHeader { Markdown = "header" },
                new DocumentParagraph { Markdown = "paragraph" },
                new DocumentTable { Markdown = "table" },
                new DocumentSection
                {
                    Markdown = "nested section",
                    Elements =
                    {
                        new DocumentHeader { Markdown = "nested header" },
                        new DocumentParagraph { Markdown = "nested paragraph" }
                    }
                }
            }
        });
        doc.Sections.Add(new DocumentSection
        {
            Markdown = "second section",
            Elements =
            {
                new DocumentHeader { Markdown = "header 2" },
                new DocumentParagraph { Markdown = "paragraph 2" }
            }
        });

        DocumentFlattener flattener = new();
        List<Document> flattened = await flattener.ProcessAsync(new List<Document> { doc });

        Document flatDoc = flattened[0];
        DocumentSection flatSection = flatDoc.Sections[0];

        Assert.Single(flattened);
        Assert.Same(doc.Markdown, flatDoc.Markdown); // The Markdown of the flattened document should be the same as the original
        Assert.Single(flatDoc.Sections); // There should be only one section in the flattened document
        Assert.Same(doc.Markdown, flatSection.Markdown); // The Markdown of the section should be the same as the whole document

        Assert.IsType<DocumentHeader>(flatSection.Elements[0]);
        Assert.Equal("header", flatSection.Elements[0].Markdown);
        Assert.IsType<DocumentParagraph>(flatSection.Elements[1]);
        Assert.Equal("paragraph", flatSection.Elements[1].Markdown);
        Assert.IsType<DocumentTable>(flatSection.Elements[2]);
        Assert.Equal("table", flatSection.Elements[2].Markdown);
        Assert.IsType<DocumentHeader>(flatSection.Elements[3]);
        Assert.Equal("nested header", flatSection.Elements[3].Markdown);
        Assert.IsType<DocumentParagraph>(flatSection.Elements[4]);
        Assert.Equal("nested paragraph", flatSection.Elements[4].Markdown);
        Assert.IsType<DocumentHeader>(flatSection.Elements[5]);
        Assert.Equal("header 2", flatSection.Elements[5].Markdown);
        Assert.IsType<DocumentParagraph>(flatSection.Elements[6]);
        Assert.Equal("paragraph 2", flatSection.Elements[6].Markdown);
    }
}
