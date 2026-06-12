using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ViciOne.ManagedEngine.ExternalCommunication;

namespace ViciOne.Suite.DataPort;

public sealed class SqlServerDataPortOutgoing(SqlServerDataPortCommunication communication, ILogger<SqlServerDataPortOutgoing> logger) : IExternalOutgoingCommunication<SqlServerDataPortCommunication>, IDisposable
{
    private readonly SqlServerRelationalDatabase _database = SqlServerRelationalDatabase.Create(communication, logger);

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
