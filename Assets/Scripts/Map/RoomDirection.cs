using System;

namespace Week14.Map
{
    [Flags]
    public enum RoomDirection
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }
}
