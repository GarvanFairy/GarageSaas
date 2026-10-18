namespace GarageSaas.UnitTests.TestHelpers;

internal sealed class SqlTestWorkers
{
    private readonly List<Task> _workers = new();
    internal T Track<T>(T task) where T : Task
    {
        lock (_workers) _workers.Add(task);
        return task;
    }
    internal async Task DrainAsync(TimeSpan timeout)
    {
        Task[] tasks; lock (_workers) tasks = _workers.ToArray();
        var all = Task.WhenAll(tasks);
        try { await all.WaitAsync(timeout); }
        catch when (all.IsCompleted) { /* Observe failures; original awaits assert them. */ }
        // Timeout does not cancel the worker. The caller must refuse deletion.
    }
}
