namespace SchoolAdmission.Api.Tests.Infrastructure;

public static class Concurrent
{
    /// <summary>
    /// Starts <paramref name="count"/> operations, holds them all at a gate, then releases them at the
    /// same moment so they genuinely race.
    /// </summary>
    public static async Task<IReadOnlyList<T>> RunAsync<T>(int count, Func<int, Task<T>> operation)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(async () =>
            {
                await gate.Task;
                return await operation(i);
            }))
            .ToList();

        await Task.Delay(100); // let every task reach the gate
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
