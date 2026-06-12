using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ViciOne.ManagedEngine.ExternalCommunication;

namespace ViciOne.Suite.DataPort;

public sealed class PostgreSqlDataPortOutgoing(PostgreSqlDataPortCommunication communication, ILogger<PostgreSqlDataPortOutgoing> logger) : IExternalOutgoingCommunication<PostgreSqlDataPortCommunication>, IDisposable
{
    private readonly PostgreSqlRelationalDatabase _database = new(communication, logger);

    public Task ConnectAsync(CancellationToken cancellationToken)
        => _database.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken)
        => _database.DisconnectAsync(cancellationToken);

    public Task SendAsync(ulong engineCycle, IReadOnlyCollection<ExternalValue> values, CancellationToken cancellationToken)
    {
        _database.Send(values);
        return Task.CompletedTask;
    }

    public void Dispose()
        => _database.Dispose();
}
