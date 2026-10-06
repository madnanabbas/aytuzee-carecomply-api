using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientTodoTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateStartsOpenAndNormalizesValues()
    {
        var todo = Create();
        Assert.Equal(ClientTodoStatus.Open, todo.Status);
        Assert.Equal("Verify service agreement", todo.Title);
        Assert.Null(todo.CompletedAtUtc);
    }

    [Fact]
    public void CompleteRecordsServerOwnedActorAndUtcTimeAndCannotRepeat()
    {
        var todo = Create(); var actor = Guid.NewGuid(); var completed = Now.AddHours(1);
        todo.Complete(actor, completed);
        Assert.Equal(ClientTodoStatus.Completed, todo.Status);
        Assert.Equal(actor, todo.CompletedByUserId);
        Assert.Equal(completed, todo.CompletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => todo.Complete(actor, completed));
    }

    [Fact]
    public void ReopenClearsCompletionMetadata()
    {
        var todo = Create(); todo.Complete(Guid.NewGuid(), Now.AddHours(1)); todo.Reopen(Now.AddHours(2));
        Assert.Equal(ClientTodoStatus.Open, todo.Status);
        Assert.Null(todo.CompletedByUserId); Assert.Null(todo.CompletedAtUtc);
    }

    [Fact]
    public void CancelledTodoCannotBeEditedOrCompleted()
    {
        var todo = Create(); todo.Cancel(Now.AddHours(1));
        Assert.Throws<InvalidOperationException>(() => todo.UpdateDetails("Changed", null, Guid.NewGuid(), null, ClientTodoPriority.Low, Now.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => todo.Complete(Guid.NewGuid(), Now.AddHours(2)));
    }

    [Fact]
    public void OverdueIsCalculatedAndCompletedTodoIsNeverOverdue()
    {
        var todo = Create();
        Assert.True(todo.IsOverdue(new DateOnly(2026, 9, 6)));
        todo.Complete(Guid.NewGuid(), Now.AddHours(1));
        Assert.False(todo.IsOverdue(new DateOnly(2026, 9, 6)));
    }

    private static ClientTodo Create() => ClientTodo.Create(Guid.NewGuid(), Guid.NewGuid(), " Verify service agreement ",
        null, Guid.NewGuid(), new DateOnly(2026, 9, 5), ClientTodoPriority.Normal, Guid.NewGuid(), Now);
}
