using System;

namespace Week14.Save
{
    [Serializable]
    public sealed class LogConsentData
    {
        public bool agreed;
        public string consentDateUtc;
    }
}
