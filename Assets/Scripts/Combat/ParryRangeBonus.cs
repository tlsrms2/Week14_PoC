namespace Week14.Combat
{
    public static class ParryRangeBonus
    {
        public static float Additive { get; private set; }

        public static void Set(float additive)
        {
            Additive = additive;
        }
    }
}
