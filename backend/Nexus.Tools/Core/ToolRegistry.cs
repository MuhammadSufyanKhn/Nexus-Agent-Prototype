using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Nexus.Tools.Core;

public interface IToolRegistry
{
    void RegisterTool(IAgentTool tool);
    IAgentTool? GetTool(string name);
    bool HasTool(string name);
    IEnumerable<ToolDefinition> GetAllDefinitions();
    IEnumerable<IAgentTool> GetAllTools();
}

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterTool(IAgentTool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        if (string.IsNullOrWhiteSpace(tool.Definition.Name))
            throw new ArgumentException("Tool definition must have a non-empty name.");

        _tools[tool.Definition.Name] = tool;
    }

    public IAgentTool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public bool HasTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _tools.ContainsKey(name);
    }

    public IEnumerable<ToolDefinition> GetAllDefinitions()
    {
        return _tools.Values.Select(t => t.Definition);
    }

    public IEnumerable<IAgentTool> GetAllTools()
    {
        return _tools.Values;
    }
}
