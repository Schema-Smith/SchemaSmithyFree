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
        public string Description { get; set; }
        public bool Deprecated { get; set; }
        public bool Required { get; set; }
        public bool SingleOrArray { get; set; }
    }
}
