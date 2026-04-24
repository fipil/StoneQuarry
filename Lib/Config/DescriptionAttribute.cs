using System;

namespace StoneQuarry.Lib.Config
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DescriptionAttribute : Attribute
    {
        public string Text { get; }

        public DescriptionAttribute(string text)
        {
            Text = text;
        }
    }
}
