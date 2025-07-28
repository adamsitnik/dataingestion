using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public abstract class DocumentReaderConformanceTests
    {
        protected abstract DocumentReader CreateDocumentReader();

        public static IEnumerable<object[]> Sources
        {
            get
            {
                yield return new object[] { "https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-NRBF/%5bMS-NRBF%5d-190313.pdf" }; // PDF file
                yield return new object[] { "https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-NRBF/%5bMS-NRBF%5d-190313.docx" }; // DOCX file
            }
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public async Task SupportsUris(string uri)
        {
            var reader = CreateDocumentReader();
            var document = await reader.ReadAsync(new Uri(uri));

            Assert.NotNull(document);
        }

        [Fact]
        public async Task ThrowsIfCancellationRequestedUrl()
        {
            var reader = CreateDocumentReader();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await reader.ReadAsync(new Uri("https://www.microsoft.com/"), cts.Token));
        }

        [Fact]
        public async Task ThrowsIfCancellationRequested()
        {
            string filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".txt");
            await File.WriteAllTextAsync(filePath, "This is a test file for cancellation token.");

            var reader = CreateDocumentReader();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            try
            {
                await Assert.ThrowsAsync<OperationCanceledException>(async () => await reader.ReadAsync(filePath, cts.Token)); // File path

                using (FileStream stream = File.OpenRead(filePath))
                {
                    await Assert.ThrowsAsync<OperationCanceledException>(async () => await reader.ReadAsync(stream, cts.Token)); // Stream
                }
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
