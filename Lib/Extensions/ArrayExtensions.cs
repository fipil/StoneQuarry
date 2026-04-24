using Vintagestory.API.Util;

namespace StoneQuarry.Lib.Extensions
{
    internal static class ArrayExtensions
    {
        public static T[] AppendIf<T>(this T[] array, bool condition, params T[] value)
        {
            if (condition)
            {
                return array.Append(value);
            }

            return array;
        }
    }
}
