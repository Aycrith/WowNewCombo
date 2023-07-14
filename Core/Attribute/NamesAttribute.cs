using System;

namespace Core;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NamesAttribute : Attribute
{
    public string[] Values { get; set; }

    public NamesAttribute(params string[] values)
    {
        Values = values;
    }
}