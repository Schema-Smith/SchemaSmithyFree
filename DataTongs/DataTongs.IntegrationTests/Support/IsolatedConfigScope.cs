// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;

namespace DataTongs.IntegrationTests.Support;

/// <summary>
/// Test-only scope that isolates per-test config mutations. Snapshots the currently
/// registered <see cref="IConfigurationRoot"/> into a fresh in-memory clone, applies the
/// test's overrides onto the clone, and registers the clone as the active config. On
/// dispose it re-registers the pristine original (or unregisters if none was registered),
/// so a test that mutates config never leaks state into sibling fixtures. Production
/// <c>ConfigHelper</c> caching is intentionally left alone.
/// </summary>
public sealed class IsolatedConfigScope : IDisposable
{
    private readonly IConfigurationRoot? _previous;

    public IConfigurationRoot Config { get; }

    private IsolatedConfigScope(IConfigurationRoot? previous, IConfigurationRoot isolated)
    {
        _previous = previous;
        Config = isolated;
    }

    public static IsolatedConfigScope Create(IDictionary<string, string?>? overrides = null)
    {
        var previous = FactoryContainer.Resolve<IConfigurationRoot>();

        var seed = previous?.AsEnumerable()
                       .Where(kvp => kvp.Value != null)
                       .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                   ?? new Dictionary<string, string?>();

        if (overrides != null)
            foreach (var kvp in overrides)
                seed[kvp.Key] = kvp.Value;

        var isolated = new ConfigurationBuilder().AddInMemoryCollection(seed).Build();
        FactoryContainer.Register<IConfigurationRoot>(isolated);
        return new IsolatedConfigScope(previous, isolated);
    }

    public void Dispose()
    {
        if (_previous != null)
            FactoryContainer.Register<IConfigurationRoot>(_previous);
        else
            FactoryContainer.Unregister<IConfigurationRoot>();
    }
}
