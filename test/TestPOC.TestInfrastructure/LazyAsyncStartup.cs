namespace TestPOC.TestInfrastructure;

/// <summary>
/// Ensures an async initialization action runs exactly once even under concurrent
/// </summary>
public sealed class LazyAsyncStartup<TState>(Func<TState, CancellationToken, Task> initializeAsync)
{
	private readonly Func<TState, CancellationToken, Task> _initializeAsync = initializeAsync;
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private bool _isInitialized;

	public async ValueTask EnsureInitialized(TState state, CancellationToken cancellationToken = default)
	{
		if (_isInitialized)
		{
			return;
		}

		await _semaphore.WaitAsync(cancellationToken);
		try
		{
			if (_isInitialized)
			{
				return;
			}

			await _initializeAsync(state, cancellationToken);
			_isInitialized = true;
		}
		finally
		{
			_semaphore.Release();
		}
	}
}
