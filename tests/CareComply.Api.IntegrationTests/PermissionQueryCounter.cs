using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareComply.Api.IntegrationTests;

public sealed class PermissionQueryCounter : DbCommandInterceptor
{
    private const string CorrelationHeader = "X-CareComply-Test-Permission-Counter";
    private readonly ConcurrentDictionary<string, CounterState> _counts =
        new(StringComparer.Ordinal);
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionQueryCounter(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public Operation BeginOperation()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var state = new CounterState();
        Assert.True(_counts.TryAdd(correlationId, state));
        return new Operation(this, correlationId, state);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Count(command);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return ValueTask.FromResult(result);
    }

    private void Count(DbCommand command)
    {
        if (!command.CommandText.Contains(
                "CareComply.PermissionResolution",
                StringComparison.Ordinal)
            || _httpContextAccessor.HttpContext is not { } httpContext
            || !httpContext.Request.Headers.TryGetValue(CorrelationHeader, out var values))
        {
            return;
        }

        var correlationId = values.ToString();
        if (_counts.TryGetValue(correlationId, out var state))
        {
            Interlocked.Increment(ref state.CommandCount);
        }
    }

    internal sealed class CounterState
    {
        public int CommandCount;
    }

    public sealed class Operation : IDisposable
    {
        private readonly PermissionQueryCounter _owner;
        private readonly string _correlationId;
        private readonly CounterState _state;
        private bool _disposed;

        internal Operation(
            PermissionQueryCounter owner,
            string correlationId,
            CounterState state)
        {
            _owner = owner;
            _correlationId = correlationId;
            _state = state;
        }

        public int CommandCount => Volatile.Read(ref _state.CommandCount);

        public void AddTo(HttpRequestMessage request) =>
            request.Headers.Add(CorrelationHeader, _correlationId);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _owner._counts.TryRemove(_correlationId, out _);
            _disposed = true;
        }
    }
}
