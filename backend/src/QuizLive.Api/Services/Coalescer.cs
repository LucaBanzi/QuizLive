namespace QuizLive.Api.Services;

/// <summary>Raggruppa molte richieste ravvicinate in una sola esecuzione "trailing" (es. contatore risposte, 200 ms).</summary>
public sealed class Coalescer(Func<Task> action, TimeSpan delay)
{
    private int _scheduled;

    public void Trigger()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            Interlocked.Exchange(ref _scheduled, 0);
            try { await action(); } catch { /* best effort */ }
        });
    }
}
