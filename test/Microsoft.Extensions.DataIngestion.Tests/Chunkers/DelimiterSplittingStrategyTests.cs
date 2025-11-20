// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class DelimiterSplittingStrategyTests : TextSplittingStrategyTests
    {
        protected override TextSplittingStrategy GetTextSplittingStrategy()
        {
            var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            return new DelimiterSplittingStrategy(tokenizer, '\n');
        }

        [Fact]
        public void TwoShortParagraphs()
        {
            TextSplittingStrategy textSplittingStrategy = GetTextSplittingStrategy();
            string text = "This is the first paragraph.\nThis is the second paragraph.";
            List<int> indices = textSplittingStrategy.GetSplitIndices(text, 200);
            Assert.Single(indices);
        }
    }
}
