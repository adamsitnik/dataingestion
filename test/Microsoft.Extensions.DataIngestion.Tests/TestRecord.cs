// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using System;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class TestRecord
{
    [VectorStoreKey(StorageName = "key")]
    public Guid Id { get; set; }

    [VectorStoreVector(Dimensions: 4, StorageName = "embedding")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "doc_id")]
    public string DocumentId { get; set; } = string.Empty;
}
