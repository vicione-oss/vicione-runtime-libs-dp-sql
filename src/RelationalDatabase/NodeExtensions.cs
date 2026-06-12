using System;
using System.Collections.Generic;
using System.Linq;

namespace ViciOne.Suite.DataPort;

public static class NodeExtensions
{
    public static bool IsJsonColumn(this INode node, Dictionary<Guid, INode[]> nodeParentsCache)
        => node.GetSuccessorRoutes(nodeParentsCache).Any(s => s.Any(e => e.DesignId == RelationalDatabaseNodeDesignId.JsonStructureNodeId));
}
