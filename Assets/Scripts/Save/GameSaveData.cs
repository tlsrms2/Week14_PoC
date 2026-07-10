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
    public sealed class ChallengeCounterEntry
    {
        public string challengeId;
        public int count;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public List<string> unlockedBossIds = new();
        public List<string> clearedBossIds = new();
        public List<string> unlockedSkillIds = new();
        public List<string> unlockedPassiveSkillIds = new();
        public List<string> unlockedWeaponIds = new();
        public List<SkillSlotData> equippedSkills = new();
        public List<SkillSlotData> equippedPassiveSkills = new();
        public string equippedWeaponId;
        public List<string> completedChallengeIds = new();
        public List<ChallengeCounterEntry> challengeCounters = new();
        public int challengePoints;
        public List<string> purchasedSkillIds = new();
        public List<string> purchasedPassiveSkillIds = new();
        public List<string> purchasedWeaponIds = new();
    }
}
