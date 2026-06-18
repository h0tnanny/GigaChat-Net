using Microsoft.SemanticKernel;

namespace GigaChat.Net.SemanticKernel;

/// <summary>Fluent builder for <see cref="GigaChatReActAgent"/>.</summary>
public sealed class GigaChatReActAgentBuilder
{
    private IGigaChatClient? _client;
    private string? _instructions;
    private int _maxToolCalls = 8;
    private double _temperature = 0.1;
    private string? _modelId;
    private GigaChatToolSafetyOptions? _toolSafety;
    private IGigaChatAgentThreadStore? _threadStore;
    private readonly List<(object Plugin, string Name)> _plugins = [];

    /// <summary>Sets the <see cref="IGigaChatClient"/> used for completions.</summary>
    public GigaChatReActAgentBuilder UseClient(IGigaChatClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        return this;
    }

    /// <summary>Creates an internal <see cref="GigaChatClient"/> from settings.</summary>
    public GigaChatReActAgentBuilder UseSettings(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _client = new GigaChatClient(settings);
        return this;
    }

    /// <summary>Sets the system instructions prepended to every run.</summary>
    public GigaChatReActAgentBuilder WithInstructions(string instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        _instructions = instructions;
        return this;
    }

    /// <summary>Overrides the maximum tool calls per run. Must be non-negative.</summary>
    public GigaChatReActAgentBuilder WithMaxToolCalls(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Maximum tool calls cannot be negative.");
        _maxToolCalls = count;
        return this;
    }

    /// <summary>Overrides the sampling temperature.</summary>
    public GigaChatReActAgentBuilder WithTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>Overrides the GigaChat model.</summary>
    public GigaChatReActAgentBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    /// <summary>Sets tool safety and error-handling options.</summary>
    public GigaChatReActAgentBuilder WithToolSafety(GigaChatToolSafetyOptions options)
    {
        _toolSafety = options;
        return this;
    }

    /// <summary>Sets the thread store for multi-turn conversation persistence.</summary>
    public GigaChatReActAgentBuilder UseThreadStore(IGigaChatAgentThreadStore store)
    {
        _threadStore = store;
        return this;
    }

    /// <summary>Registers a plugin for auto-invocation.</summary>
    public GigaChatReActAgentBuilder AddPlugin(object plugin, string pluginName)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(pluginName));
        if (_plugins.Any(p => string.Equals(p.Name, pluginName, StringComparison.Ordinal)))
            throw new ArgumentException($"A plugin named '{pluginName}' has already been added.", nameof(pluginName));
        _plugins.Add((plugin, pluginName));
        return this;
    }

    /// <summary>Registers a pre-built <see cref="KernelPlugin"/>.</summary>
    public GigaChatReActAgentBuilder AddKernelPlugin(KernelPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (_plugins.Any(p => string.Equals(p.Name, plugin.Name, StringComparison.Ordinal)))
            throw new ArgumentException($"A plugin named '{plugin.Name}' has already been added.", nameof(plugin));
        _plugins.Add((plugin, plugin.Name));
        return this;
    }

    internal GigaChatReActAgent Build()
    {
        if (_client is null)
            throw new InvalidOperationException(
                "A GigaChat client is required. Call UseClient() or UseSettings() before building.");

        var options = new GigaChatReActAgentOptions
        {
            MaxToolCalls = _maxToolCalls,
            Temperature = _temperature,
            Instructions = _instructions,
            ToolSafety = _toolSafety,
            ModelId = _modelId
        };

        var kernel = Kernel.CreateBuilder().Build();
        foreach (var (plugin, name) in _plugins)
        {
            if (plugin is KernelPlugin kernelPlugin)
                kernel.Plugins.Add(kernelPlugin);
            else
                kernel.Plugins.AddFromObject(plugin, name);
        }

        return new GigaChatReActAgent(_client, kernel, options, _threadStore);
    }
}
