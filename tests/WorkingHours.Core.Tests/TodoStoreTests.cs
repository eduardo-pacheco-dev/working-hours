using WorkingHours.Core.Services;

namespace WorkingHours.Core.Tests;

public sealed class TodoStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WorkingHoursTodo-" + Guid.NewGuid().ToString("N"));

    public TodoStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private TodoStore CreateStore() => new(Path.Combine(_root, "todos.json"));

    [Fact]
    public void Add_TrimsAndIgnoresBlankText()
    {
        var store = CreateStore();

        Assert.Null(store.Add("   "));
        Assert.Null(store.Add(""));

        var item = store.Add("  Revisar proposta  ");
        Assert.NotNull(item);
        Assert.Equal("Revisar proposta", item!.Text);
        Assert.False(item.IsDone);
    }

    [Fact]
    public void Tasks_PersistAcrossStoreInstances()
    {
        var path = Path.Combine(_root, "todos.json");
        var store = new TodoStore(path);
        store.Add("Primeira");
        store.Add("Segunda");

        var reloaded = new TodoStore(path).GetAll();

        Assert.Equal(2, reloaded.Count);
        Assert.Equal("Primeira", reloaded[0].Text);
        Assert.Equal("Segunda", reloaded[1].Text);
    }

    [Fact]
    public void Update_ChangesDoneState()
    {
        var store = CreateStore();
        var item = store.Add("Ligar para o cliente")!;
        item.IsDone = true;

        store.Update(item);

        var reloaded = store.GetAll().Single();
        Assert.True(reloaded.IsDone);
    }

    [Fact]
    public void Delete_RemovesOnlyTheRequestedTask()
    {
        var store = CreateStore();
        var first = store.Add("A")!;
        store.Add("B");

        store.Delete(first.Id);

        var remaining = store.GetAll();
        Assert.Single(remaining);
        Assert.Equal("B", remaining[0].Text);
    }

    [Fact]
    public void ClearCompleted_RemovesDoneTasksOnly()
    {
        var store = CreateStore();
        var done = store.Add("Feita")!;
        store.Add("Pendente");
        done.IsDone = true;
        store.Update(done);

        store.ClearCompleted();

        var remaining = store.GetAll();
        Assert.Single(remaining);
        Assert.Equal("Pendente", remaining[0].Text);
    }

    [Fact]
    public void Update_PersistsTrackedTimeAndCompletion()
    {
        var store = CreateStore();
        var item = store.Add("Preparar orçamento")!;
        var startedAt = DateTime.Today.AddHours(9);
        var completedAt = DateTime.Today.AddHours(10);

        item.TimeSpentSeconds = 3725;
        item.TimerStartedAt = startedAt;
        item.CompletedAt = completedAt;
        item.IsDone = true;
        store.Update(item);

        var reloaded = store.GetAll().Single();
        Assert.Equal(3725, reloaded.TimeSpentSeconds);
        Assert.Equal(startedAt, reloaded.TimerStartedAt);
        Assert.Equal(completedAt, reloaded.CompletedAt);
        Assert.True(reloaded.IsDone);
    }

    [Fact]
    public void Reorder_PersistsManualOrder()
    {
        var store = CreateStore();
        var first = store.Add("Primeira")!;
        var second = store.Add("Segunda")!;
        var third = store.Add("Terceira")!;

        store.Reorder(new[] { third.Id, first.Id, second.Id });

        var reloaded = store.GetAll();
        Assert.Equal(new[] { "Terceira", "Primeira", "Segunda" }, reloaded.Select(t => t.Text));
    }

    [Fact]
    public void Reorder_KeepsTasksMissingFromTheListAtTheEnd()
    {
        var store = CreateStore();
        var first = store.Add("Primeira")!;
        store.Add("Segunda");
        var third = store.Add("Terceira")!;

        store.Reorder(new[] { third.Id, first.Id });

        var reloaded = store.GetAll();
        Assert.Equal(new[] { "Terceira", "Primeira", "Segunda" }, reloaded.Select(t => t.Text));
    }

    [Fact]
    public void MissingFile_ReturnsEmptyList()
    {
        var store = CreateStore();
        Assert.Empty(store.GetAll());
    }
}
