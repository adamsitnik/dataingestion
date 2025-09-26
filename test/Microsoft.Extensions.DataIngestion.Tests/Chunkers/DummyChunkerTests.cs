// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    public class DummyChunkerTests : DocumentChunkerTests
    {
        protected override IDocumentChunker CreateDocumentChunker()
        {
            return new DummyChunker();
        }
    }
}
