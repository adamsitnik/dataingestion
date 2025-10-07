// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Represents a processor that removes specific elements from a document based on a provided predicate.
/// </summary>
public sealed class RemovalProcessor : IDocumentProcessor
{
    public static RemovalProcessor Footers { get; } = new(static element => element is DocumentFooter);
    public static RemovalProcessor EmptySections { get; } = new(static element => element is DocumentSection section && section.Elements.Count == 0);

    private readonly Predicate<DocumentElement> _shouldRemove;

    public RemovalProcessor(Predicate<DocumentElement> shouldRemove) => _shouldRemove = shouldRemove;

    public Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        Document updated = new(document.Identifier);

        foreach (DocumentSection section in document.Sections)
        {
            if (Process(section) is DocumentSection updatedSection)
            {
                updated.Sections.Add(updatedSection);
            }
        }

        return Task.FromResult(updated);
    }

    private DocumentElement? Process(DocumentElement element)
    {
        if (_shouldRemove(element))
        {
            return null;
        }
        else if (element is DocumentSection section)
        {
            DocumentSection updatedSection = new();
            foreach (DocumentElement child in section.Elements)
            {
                if (Process(child) is DocumentElement updatedChild)
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
