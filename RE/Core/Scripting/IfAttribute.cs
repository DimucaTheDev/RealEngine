using System;
using System.Collections.Generic;
using System.Text;

namespace RE.Core.Scripting
{
    //todo: docs
    //this attribute is used to specify that a property should only be displayed if property NAME matches VALUE
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class IfAttribute(string v, object? val) : Attribute
    {
        public string Name { get; } = v;
        public object? Value { get; } = val;
    }
}
