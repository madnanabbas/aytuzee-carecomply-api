using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientTodos;
using CareComply.Domain.Clients;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/todos")]
public sealed class ClientTodosController(ISender sender) : ControllerBase
{
    [HttpGet("assignees")]
    [HasPermission(Permissions.ClientTodo.View)]
    public async Task<ActionResult<IReadOnlyList<ClientTodoUserResponse>>> Assignees(
        CancellationToken ct
    )
    {
        var result = await sender.Send(new ListClientTodoAssigneesQuery(), ct);
        if (result.AuthorizationFailure is { } failure)
            return MapAuthorizationFailure(failure);
        return Ok(
            (result.Assignees ?? [])
                .Select(x => new ClientTodoUserResponse(x.Id, x.DisplayName))
                .ToArray()
        );
    }

    [HttpGet]
    [HasPermission(Permissions.ClientTodo.View)]
    public async Task<ActionResult<IReadOnlyList<ClientTodoResponse>>> List(
        Guid clientId,
        CancellationToken ct
    )
    {
        var result = await sender.Send(new ListClientTodosQuery(clientId), ct);
        if (result.AuthorizationFailure is { } failure)
            return MapAuthorizationFailure(failure);
        return result.Todos is { } todos ? Ok(todos.Items.Select(Map).ToArray()) : ClientNotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.ClientTodo.Create)]
    public async Task<ActionResult<ClientTodoResponse>> Create(
        Guid clientId,
        CreateClientTodoRequest request,
        CancellationToken ct
    )
    {
        if (!TryEnum(request.Priority, out ClientTodoPriority priority))
            return InvalidEnum(
                nameof(request.Priority),
                request.Priority,
                typeof(ClientTodoPriority)
            );
        var result = await sender.Send(
            new CreateClientTodoCommand(
                clientId,
                request.Title,
                request.Description,
                request.AssignedToUserId,
                request.DueDate,
                priority
            ),
            ct
        );
        return MapMutation(result, created: true);
    }

    [HttpPut("{todoId:guid}")]
    [HasPermission(Permissions.ClientTodo.Edit)]
    public async Task<ActionResult<ClientTodoResponse>> Update(
        Guid clientId,
        Guid todoId,
        UpdateClientTodoRequest request,
        CancellationToken ct
    )
    {
        if (!TryEnum(request.Priority, out ClientTodoPriority priority))
            return InvalidEnum(
                nameof(request.Priority),
                request.Priority,
                typeof(ClientTodoPriority)
            );
        return MapMutation(
            await sender.Send(
                new UpdateClientTodoCommand(
                    clientId,
                    todoId,
                    request.Title,
                    request.Description,
                    request.AssignedToUserId,
                    request.DueDate,
                    priority,
                    request.EditorVersion
                ),
                ct
            )
        );
    }

    [HttpPost("{todoId:guid}/complete")]
    [HasPermission(Permissions.ClientTodo.Complete)]
    public async Task<ActionResult<ClientTodoResponse>> Complete(
        Guid clientId,
        Guid todoId,
        ClientTodoTransitionRequest request,
        CancellationToken ct
    ) =>
        MapMutation(
            await sender.Send(
                new CompleteClientTodoCommand(clientId, todoId, request.EditorVersion),
                ct
            )
        );

    [HttpPost("{todoId:guid}/reopen")]
    [HasPermission(Permissions.ClientTodo.Reopen)]
    public async Task<ActionResult<ClientTodoResponse>> Reopen(
        Guid clientId,
        Guid todoId,
        ClientTodoTransitionRequest request,
        CancellationToken ct
    ) =>
        MapMutation(
            await sender.Send(
                new ReopenClientTodoCommand(clientId, todoId, request.EditorVersion),
                ct
            )
        );

    [HttpPost("{todoId:guid}/cancel")]
    [HasPermission(Permissions.ClientTodo.Cancel)]
    public async Task<ActionResult<ClientTodoResponse>> Cancel(
        Guid clientId,
        Guid todoId,
        ClientTodoTransitionRequest request,
        CancellationToken ct
    ) =>
        MapMutation(
            await sender.Send(
                new CancelClientTodoCommand(clientId, todoId, request.EditorVersion),
                ct
            )
        );

    private ActionResult<ClientTodoResponse> MapMutation(
        ClientTodoOperationResult result,
        bool created = false
    )
    {
        if (result.AuthorizationFailure is { } failure)
            return MapAuthorizationFailure(failure);
        return result.Mutation?.Status switch
        {
            ClientTodoMutationStatus.Succeeded when result.Mutation.Todo is { } todo => created
                ? StatusCode(StatusCodes.Status201Created, Map(todo))
                : Ok(Map(todo)),
            ClientTodoMutationStatus.ClientNotFound => ClientNotFound(),
            ClientTodoMutationStatus.TodoNotFound => Problem(
                statusCode: 404,
                title: "The requested client todo was not found.",
                type: ProblemTypes.ClientTodoNotFound
            ),
            ClientTodoMutationStatus.InvalidAssignee => Problem(
                statusCode: 400,
                title: "The selected assignee is not an active user.",
                type: ProblemTypes.ClientTodoInvalidAssignee
            ),
            ClientTodoMutationStatus.InvalidState => Problem(
                statusCode: 409,
                title: "The requested todo state transition is not allowed.",
                type: ProblemTypes.ClientTodoInvalidState
            ),
            ClientTodoMutationStatus.Conflict => Problem(
                statusCode: 409,
                title: "The todo changed since it was loaded. Refresh and try again.",
                type: ProblemTypes.ClientTodoConflict
            ),
            _ => Problem(
                statusCode: 409,
                title: "The client todo could not be changed.",
                type: ProblemTypes.ClientTodoConflict
            ),
        };
    }

    private ObjectResult ClientNotFound() =>
        Problem(
            statusCode: 404,
            title: "The requested client was not found.",
            type: ProblemTypes.ClientNotFound
        );

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private BadRequestObjectResult InvalidEnum(string field, string? value, Type type) =>
        BadRequest(
            new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [field] =
                    [
                        $"'{value ?? string.Empty}' is invalid. Allowed values: {string.Join(", ", Enum.GetNames(type))}.",
                    ],
                }
            )
            {
                Status = 400,
                Title = "One or more validation errors occurred.",
                Type = ProblemTypes.Validation,
            }
        );

    private static bool TryEnum<T>(string? value, out T parsed)
        where T : struct, Enum => Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);

    private static ClientTodoResponse Map(ClientTodoItem x) =>
        new(
            x.Id,
            x.Title,
            x.Description,
            new(x.AssignedToUserId, x.AssignedToDisplayName),
            x.DueDate,
            x.Priority.ToString(),
            x.Status.ToString(),
            x.IsOverdue,
            new(x.CreatedByUserId, x.CreatedByDisplayName),
            x.CreatedAtUtc,
            x.UpdatedAtUtc,
            x.CompletedAtUtc,
            x.EditorVersion
        );
}
