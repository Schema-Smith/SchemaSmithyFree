// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Domain
{
    /// <summary>
    /// Token resolution frequency for the schema-templates fan-out engine (design §5.6).
    /// The engine cannot safely DEMOTE a token's resolution frequency — a &lt;*Query*&gt; token
    /// without <c>{{SchemaName}}</c> may legitimately depend on DB-specific state. The dependency
    /// walk only ever ESCALATES a token's scope when its body (directly or transitively) references
    /// <c>{{SchemaName}}</c>.
    /// </summary>
    public enum TokenScope : ushort
    {
        /// <summary>Fully static, no &lt;*Query*&gt; — resolved once per product (today's behavior).</summary>
        PerProduct,

        /// <summary>
        /// &lt;*Query*&gt; token with no <c>{{SchemaName}}</c> reference — resolved per DB
        /// (today's default for query tokens, unchanged).
        /// </summary>
        PerDb,

        /// <summary>
        /// Body references <c>{{SchemaName}}</c> directly or transitively via another token —
        /// resolved per (DB, schema iteration).
        /// </summary>
        Iteration
    }
}
