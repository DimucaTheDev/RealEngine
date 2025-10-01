using System;
using System.Collections.Generic;
using System.Text;

namespace RE.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class GlPropertyNameAttribute(string name) : Attribute
    {
        public string PropertyName { get; set; } = name;
    }
}
