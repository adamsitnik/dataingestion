// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class MarkdownReaderTests : DocumentReaderConformanceTests
{
    protected override DocumentReader CreateDocumentReader(bool extractImages = false) => new MarkdownReader();

    public static new IEnumerable<object[]> Sources
    {
        get
        {
            yield return new object[] { "https://raw.githubusercontent.com/microsoft/markitdown/main/README.md", "README.md" };
        }
    }

    public static new IEnumerable<object[]> Files
    {
        get
        {
            yield return new object[] { Path.Combine("TestFiles", "Sample.md"), "Sample.md" };
        }
    }

    public static new IEnumerable<object[]> Images
    {
        get
        {
            yield return new object[] { Path.Combine("TestFiles", "SampleWithImage.md"), "SampleWithImage.md" };
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public override Task SupportsUris(string uri, string expectedId) => base.SupportsUris(uri, expectedId);

    [Theory]
    [MemberData(nameof(Files))]
    public override Task SupportsFiles(string filePath, string expectedId) => base.SupportsFiles(filePath, expectedId);

    [Theory]
    [MemberData(nameof(Images))]
    public override Task SupportsImages(string filePath, string expectedId) => base.SupportsImages(filePath, expectedId);
}
