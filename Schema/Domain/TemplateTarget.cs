// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;

namespace Schema.Domain
{
    /// <summary>
    /// Configuration-driven per-template fan-out override. When present in SchemaQuench
    /// settings under <c>Target.TemplateTargets</c>, REPLACES the corresponding identification
    /// script's result for the named template at runtime. Package-side fan-out type detection
    /// (<see cref="Template.IsSchemaTemplate"/>) stays tied to script presence in
    /// <c>Template.json</c>; this type is purely a runtime substitution mechanism.
    /// </summary>
    public class TemplateTarget
    {
        /// <summary>
        /// Database names that REPLACE this template's <c>DatabaseIdentificationScript</c>
        /// result. Validation requires the template to declare <c>DatabaseIdentificationScript</c>
        /// when this list is non-empty (design §5 rule 5).
        /// </summary>
        public List<string> Databases { get; set; } = new();

        /// <summary>
        /// Schema names that REPLACE this template's <c>SchemaIdentificationScript</c> result.
        /// Validation requires the template to declare <c>SchemaIdentificationScript</c> when
        /// this list is non-empty (design §5 rule 4).
        /// </summary>
        public List<string> Schemas { get; set; } = new();

        /// <summary>
        /// When true, entries in <see cref="Databases"/> / <see cref="Schemas"/> that don't yet
        /// exist on the target are provisioned via platform-appropriate <c>CREATE SCHEMA</c> /
        /// <c>CREATE DATABASE</c>. When false (default), missing entries are skipped with an
        /// info log and only existing entries are deployed.
        /// </summary>
        public bool CreateIfMissing { get; set; }

        /// <summary>
        /// True when neither <see cref="Databases"/> nor <see cref="Schemas"/> has any entries.
        /// Validation treats this as a config error (design §5 rule 3).
        /// </summary>
        public bool HasNoTargets => !Databases.Any() && !Schemas.Any();
    }
}
