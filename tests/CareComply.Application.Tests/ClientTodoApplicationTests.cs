using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientTodos;
using CareComply.Domain.Clients;

namespace CareComply.Application.Tests;

public sealed class ClientTodoApplicationTests
{
    [Fact]
    public void MessagesUseCanonicalPermissions()
    {
        var clientId = Guid.NewGuid(); var todoId = Guid.NewGuid(); var version = new string('A', 64);
        Assert.Equal(Permissions.ClientTodo.View, new ListClientTodosQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientTodo.Create, new CreateClientTodoCommand(clientId, "Task", null, Guid.NewGuid(), null, ClientTodoPriority.Normal).RequiredPermission);
        Assert.Equal(Permissions.ClientTodo.Edit, new UpdateClientTodoCommand(clientId, todoId, "Task", null, Guid.NewGuid(), null, ClientTodoPriority.Normal, version).RequiredPermission);
        Assert.Equal(Permissions.ClientTodo.Complete, new CompleteClientTodoCommand(clientId, todoId, version).RequiredPermission);
        Assert.Equal(Permissions.ClientTodo.Reopen, new ReopenClientTodoCommand(clientId, todoId, version).RequiredPermission);
        Assert.Equal(Permissions.ClientTodo.Cancel, new CancelClientTodoCommand(clientId, todoId, version).RequiredPermission);
    }

    [Fact]
    public void AuditMetadataExcludesTitleAndDescription()
    {
        var command = new CreateClientTodoCommand(Guid.NewGuid(), "Sensitive task", "Sensitive description", Guid.NewGuid(), null, ClientTodoPriority.Normal);
        var result = ClientTodoOperationResult.Completed(new(CareComply.Application.Abstractions.Clients.ClientTodoMutationStatus.ClientNotFound));
        var audit = command.CreateAuditEvent(result);
        Assert.Equal("ClientTodo.Created", audit.Action);
        Assert.DoesNotContain("Sensitive", audit.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorRejectsInvalidCreateRequest()
    {
        var command = new CreateClientTodoCommand(Guid.Empty, "", null, Guid.Empty, null, (ClientTodoPriority)99);
        var result = new CreateClientTodoCommandValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "Title");
        Assert.Contains(result.Errors, x => x.PropertyName == "AssignedToUserId");
        Assert.Contains(result.Errors, x => x.PropertyName == "Priority");
    }
}
