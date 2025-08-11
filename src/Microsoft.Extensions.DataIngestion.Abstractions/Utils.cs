// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq; 

namespace Microsoft.Extensions.DataIngestion
{
    internal static class Utils
    {
        internal static string ConcatMarkdown(IEnumerable<IContentElement> elements)
        {
            return string.Join(Environment.NewLine, elements.Select(e => e.Markdown));
        }
    }
}
