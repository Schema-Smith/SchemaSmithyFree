// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Capabilities
{
    /// <summary>
    /// How a version-gated feature's declared form behaves below its introduction version. Only features
    /// that genuinely change end-state below a version are catalogued (see <see cref="CapabilityRegistry"/>);
    /// a feature supported via a different mechanism with the same end-state is not a degrade and is excluded.
    /// </summary>
    public enum DegradeKind
    {
        /// <summary>The declared feature is omitted entirely below the floor — e.g. a temporal / masked /
        /// encrypted / columnstore declaration is not applied, or a CHECK / NULLS NOT DISTINCT clause is dropped.</summary>
        Skip,

        /// <summary>The object is created but with reduced fidelity below the floor — e.g. a descending index
        /// key part is stored ascending.</summary>
        Reduced
    }
}
