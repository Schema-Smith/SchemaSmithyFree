// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Domain
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SchemaPropertyAttribute : Attribute
    {
        public string Pattern { get; set; }
        public double Minimum { get; set; } = double.NaN;
        public double Maximum { get; set; } = double.NaN;
        public double MultipleOf { get; set; } = double.NaN;
        public string Format { get; set; }
        public int MaxLength { get; set; } = -1;
        public string Description { get; set; }
        public bool Deprecated { get; set; }
        public bool Required { get; set; }

        // Name of a sibling bool property that makes Required conditional: the property is required
        // UNLESS that bool is true. Emitted as a JSON Schema if/else so editors keep flagging the
        // ordinary case. Ignored on types that do not declare the named property, where the plain
        // Required applies -- an engine without the concept has no exception to make.
        public string RequiredUnless { get; set; }
        public bool SingleOrArray { get; set; }
    }
}
