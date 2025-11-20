// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    internal static class ChunkingHelpers
    {
        internal static void ThrowTokenCountExceeded()
                => throw new InvalidOperationException("Can't fit in the current chunk. Consider increasing max tokens per chunk.");

        internal static string GetDocumentMarkdown(IngestionDocument document)
        {
            StringBuilder sb = new();
            for (int i = 0; i < document.Sections.Count; i++)
            {
                sb.Append(document.Sections[i].GetMarkdown());
                if (i != document.Sections.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
