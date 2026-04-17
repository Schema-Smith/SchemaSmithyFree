// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Delivery;

/// <summary>
/// Minimal column contract for data delivery. Column nullability determines whether
/// FK columns can be NULLed in pass 1 of two-pass delivery (deferred dependency
/// handling). Implemented by the Column class.
/// </summary>
public interface IDeliverableColumn
{
    string Name { get; }
    bool Nullable { get; }
}
