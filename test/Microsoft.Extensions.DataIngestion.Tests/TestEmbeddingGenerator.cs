// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class TestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public bool WasCalled { get; private set; } = false;

    public void Dispose() { }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        WasCalled = true;

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new(new float[] { 0, 1, 2, 3 })]));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
