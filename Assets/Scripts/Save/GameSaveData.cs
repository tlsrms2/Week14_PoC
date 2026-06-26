using System;
using System.Collections.Generic;

namespace Week14.Save
{
    [Serializable]
    public sealed class SkillSlotData
    {
        public int slot;
        public string skillId;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public List<string> unlockedBossIds = new();
        public List<string> clearedBossIds = new();
        public List<string> unlockedSkillIds = new();
        public List<string> unlockedWeaponIds = new();
        public List<SkillSlotData> equippedSkills = new();
        public string equippedWeaponId;
    }
}
