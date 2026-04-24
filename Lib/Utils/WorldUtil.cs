using Vintagestory.API.Common;

namespace StoneQuarry.Lib.Utils
{
    internal static class WorldUtil
    {
        public static CollectibleObject GetCollectibleObject(this IWorldAccessor world, AssetLocation code)
        {
            return (CollectibleObject)world.GetItem(code) ?? world.GetBlock(code);
        }
    }
}
