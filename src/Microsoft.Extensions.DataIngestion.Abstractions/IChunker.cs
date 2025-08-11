using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion
{
    /// <summary>
    /// Defines a contract for splitting a <see cref="Document"/> into smaller, more manageable chunks.
    /// </summary>
    public interface IChunker
    {
        /// <summary>
        /// Synchronously splits the given <paramref name="document"/> into chunks according to the chunker strategy.
        /// </summary>
        /// <param name="document">The source document to chunk.</param>
        /// <returns>A collection of <see cref="Chunk"/> objects representing the chunks.</returns>
        IEnumerable<Chunk> Chunk(Document document);

        ///// <summary>
        ///// Asynchronously splits the given <paramref name="document"/> into chunks according to the chunker strategy.
        ///// </summary>
        ///// <param name="document">The source document to chunk.</param>
        ///// <param name="cancellationToken">A token to cancel the operation.</param>
        ///// <returns>A task that represents the asynchronous operation and yields a collection of <see cref="Chunk"/> objects representing the chunks.</returns>
        //Task<IEnumerable<Chunk>> ChunkAsync(Document document, CancellationToken cancellationToken = default);
    }
}
