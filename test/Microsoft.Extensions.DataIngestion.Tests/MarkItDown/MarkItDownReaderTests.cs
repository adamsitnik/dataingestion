// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class MarkItDownReaderTests : DocumentReaderConformanceTests
{
    protected override DocumentReader CreateDocumentReader() => new MarkItDownReader();

    protected override void SimpleAsserts(Document document, string source)
    {
        Assert.NotNull(document);
        Assert.NotEmpty(document.Sections);
        Assert.NotEmpty(document.Markdown);

        var elements = Flatten(document).ToArray();
        
        bool isPdf = source.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdf)
        {
            // MarkItDown does a bad job of recognizing Headers and Tables even for simple PDF files.
            Assert.Contains(elements, element => element is Header);
            Assert.Contains(elements, element => element is Table);
        }

        Assert.Contains(elements, element => element is Paragraph);
        Assert.All(elements, element => Assert.NotEmpty(element.Markdown));
    }
}
