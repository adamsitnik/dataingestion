// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Represents a processor that removes specific elements from a document based on a provided predicate.
/// </summary>
public sealed class RemovalProcessor : IngestionDocumentProcessor
{
    public static RemovalProcessor Footers { get; } = new(static element => element is IngestionDocumentFooter);
    public static RemovalProcessor EmptySections { get; } = new(static element => element is IngestionDocumentSection section && section.Elements.Count == 0);

    private readonly Predicate<IngestionDocumentElement> _shouldRemove;

    public RemovalProcessor(Predicate<IngestionDocumentElement> shouldRemove) => _shouldRemove = shouldRemove;

    public override Task<IngestionDocument> ProcessAsync(IngestionDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        IngestionDocument updated = new(document.Identifier);

        foreach (IngestionDocumentSection section in document.Sections)
        {
            if (Process(section) is IngestionDocumentSection updatedSection)
            {
                updated.Sections.Add(updatedSection);
            }
        }

        return Task.FromResult(updated);
    }

    private IngestionDocumentElement? Process(IngestionDocumentElement element)
    {
        if (_shouldRemove(element))
        {
            return null;
        }
        else if (element is IngestionDocumentSection section)
        {
            IngestionDocumentSection updatedSection = new();
            foreach (IngestionDocumentElement child in section.Elements)
            {
                if (Process(child) is IngestionDocumentElement updatedChild)
                {
                    updatedSection.Elements.Add(updatedChild);
                }
            }

            // We need to check again if we should remove the section, as it might be empty now.
            return _shouldRemove(updatedSection) ? null : updatedSection;
        }

        return element;
    }
}
