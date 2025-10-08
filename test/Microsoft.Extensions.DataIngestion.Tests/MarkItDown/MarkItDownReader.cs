// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class MarkItDownReader : DocumentReader
{
    private readonly string _exePath;
    private readonly bool _extractImages;

    public MarkItDownReader(string exePath = "markitdown", bool extractImages = false)
    {
        _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
        _extractImages = extractImages;
    }

    public override async Task<IngestionDocument> ReadAsync(string filePath, string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = _exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        // Force UTF-8 encoding in the environment (will produce garbage otherwise).
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["LC_ALL"] = "C.UTF-8";
        startInfo.Environment["LANG"] = "C.UTF-8";

        startInfo.ArgumentList.Add(filePath);

        if (_extractImages)
        {
            startInfo.ArgumentList.Add("--keep-data-uris");
        }

        string outputContent = "";
        using (Process process = new() { StartInfo = startInfo })
        {
            process.Start();

            // Read standard output asynchronously
            outputContent = await process.StandardOutput.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"MarkItDown process failed with exit code {process.ExitCode}.");
            }
        }

        return MarkdownReader.Parse(outputContent, identifier);
    }

    public override async Task<IngestionDocument> ReadAsync(Uri source, string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.GetAsync(source, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Instead of creating a temporary file, we could write to the StandardInput of the process.
        // MarkItDown says it supports reading from stdin, but it does not work as expected.
        // Even the sample command line does not work with stdin: "cat example.pdf | markitdown"
        // I can be doing something wrong, but for now, let's write to a temporary file.

        string inputFilePath = Path.GetTempFileName();
        using (FileStream inputFile = new(inputFilePath, FileMode.Open, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.Asynchronous))
        {
            await response.Content.CopyToAsync(inputFile, cancellationToken);
        }

        try
        {
            return await ReadAsync(inputFilePath, identifier, cancellationToken);
        }
        finally
        {
            File.Delete(inputFilePath);
        }
    }
}
