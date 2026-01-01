using System.Threading;
using System.Threading.Tasks;

namespace FinanceManager.Infrastructure.Setup
{
    /// <summary>
    /// Service responsible for performing automatic initialization tasks when the application starts.
    /// Implementations can run synchronous or asynchronous initialization logic such as seeding default data,
    /// creating required system records or performing one-time migrations.
    /// </summary>
    public interface IAutoInitializationService
    {
        /// <summary>
        /// Runs initialization logic synchronously.
        /// This call may perform long-running operations and can throw exceptions when initialization fails.
        /// Prefer <see cref="RunAsync(CancellationToken)"/> for cancellable asynchronous execution.
        /// </summary>
        /// <exception cref="System.Exception">Implementation-specific exceptions may be thrown when initialization fails.</exception>
        void Run();

        /// <summary>
        /// Runs initialization logic asynchronously.
        /// </summary>
        /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when initialization has finished.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        /// <exception cref="System.Exception">Implementation-specific exceptions may be thrown when initialization fails.</exception>
        Task RunAsync(CancellationToken ct);
    }
}
