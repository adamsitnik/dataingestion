// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using LlamaParse;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class LlamaParseReaderTests : DocumentReaderConformanceTests
{
    protected override DocumentReader CreateDocumentReader(bool extractImages = false)
    {
        string key = Environment.GetEnvironmentVariable("LLAMACLOUD_API_KEY")!;

        LlamaParse.Configuration configuration = new()
        {
            ApiKey = key ?? throw new InvalidOperationException("LLAMACLOUD_API_KEY environment variable is not set."),
            ItemsToExtract = ItemType.Image | ItemType.Table,
        };

        return new LlamaParseReader(new LlamaParseClient(new HttpClient(), configuration));
    }
}
