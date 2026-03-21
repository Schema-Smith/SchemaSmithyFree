using System;

namespace Schema.Domain;

[AttributeUsage(AttributeTargets.Property)]
public class SchemaPropertyAttribute : Attribute
{
    public bool Required { get; set; }
    public string Pattern { get; set; }
    public double Minimum { get; set; } = double.NaN;
    public double Maximum { get; set; } = double.NaN;
}
