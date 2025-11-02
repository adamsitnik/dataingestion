// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public abstract class TextSplittingStrategy
    {

        /// <summary>
        /// Indices in the text where splits should occur based on the maxTokenCount.
        /// </summary>
        /// <param name="text">Text to be split</param>
        /// <param name="maxTokenCount">Maximum tokens per chunk.</param>
        /// <returns></returns>
        public abstract List<int> GetSplitIndices(ReadOnlySpan<char> text, int maxTokenCount);
    }
}
