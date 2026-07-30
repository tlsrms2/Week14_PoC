namespace Week14.Audio
{
    /// <summary>
    /// 프로젝트에서 이름을 고정해 사용하는 SoundLibrary ID 계약입니다.
    /// ID를 바꿀 때는 이 목록과 SoundLibrary 항목을 함께 변경해야 합니다.
    /// </summary>
    public enum SoundId
    {
        ButtonClick,
        TalkERIS,
        TalkLeeOn,
        TalkDad,
        ResultPanelPopup,
        Intro_ZoomIn,
        Intro_Set,
        Intro_Shutter,
        BaseballBatCharge,
        BaseballBatSwing,
        BaseballBatHit,
        Boss_CreateParrySuppressionBait,
        Boss_Step,
        Boss_BeforeDash,
        Boss_CantParryingShot,
        Module_Emergency,
        Module_Magnet,
        Module_Parrying,
        Hog_MachinegunCharge,
        Hog_Death,
        Muscle_LineFire,
        Muscle_Death,
        Conductor_Draw,
        Conductor_DrawComplete,
        Conductor_DroneIdle,
        Conductor_PlaceStaff,
        Conductor_Death,
        Assassin_Stealth,
        Assassin_Mine,
        Assassin_Death,
        Hacker_Walk,
        Hacker_Slash,
        Hacker_Lift,
        Hacker_Slam,
        Hacker_SlashMiss,
        Hacker_SnipierCharge,
        Hacker_ChargeDash,
        Hacker_DropWeapon,
        Hacker_OrbitSweep,
        Hacker_WireNode,
        Hacker_Death
    }

    public static class SoundIdExtensions
    {
        public static string ToLibraryId(this SoundId id)
        {
            return id.ToString();
        }

        public static bool TryGetTalkSoundId(string speaker, out SoundId soundId)
        {
            string key = string.IsNullOrWhiteSpace(speaker)
                ? string.Empty
                : speaker.Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .ToUpperInvariant();

            switch (key)
            {
                case "ERIS":
                case "에리스":
                    soundId = SoundId.TalkERIS;
                    return true;

                case "LEEON":
                case "리온":
                    soundId = SoundId.TalkLeeOn;
                    return true;

                case "DAD":
                case "FATHER":
                case "아빠":
                    soundId = SoundId.TalkDad;
                    return true;

                default:
                    soundId = default;
                    return false;
            }
        }

        public static bool TryGetTalkSoundEvent(
            string speaker,
            out SoundEvent soundEvent)
        {
            if (!TryGetTalkSoundId(speaker, out SoundId soundId))
            {
                soundEvent = default;
                return false;
            }

            soundEvent = soundId switch
            {
                SoundId.TalkERIS => SoundEvent.Dialogue_TalkERIS,
                SoundId.TalkLeeOn => SoundEvent.Dialogue_TalkLeeOn,
                SoundId.TalkDad => SoundEvent.Dialogue_TalkDad,
                _ => default
            };
            return soundId is SoundId.TalkERIS
                or SoundId.TalkLeeOn
                or SoundId.TalkDad;
        }
    }
}
