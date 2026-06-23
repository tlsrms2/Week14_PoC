using System;
using System.Collections.Generic;

namespace Week14.Save
{
    [Serializable]
    public sealed class BossSaveData
    {
        public List<string> unlockedBossIds = new();
        public List<string> clearedBossIds = new();
    }
}
