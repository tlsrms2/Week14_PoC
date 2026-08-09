using System;
using System.IO;
using UnityEngine;

namespace Week14.Save
{
    // 로그 수집 동의 기능 자체를 뺴면서 더 이상 사용하지 않음(LogConsentPanelView와 함께 정리). 참고용으로 보관.
    // 로그 수집 동의/거부 여부를 저장/조회합니다. 응답 파일이 존재하면 이미 응답한 것으로 간주합니다.
    public static class LogConsentManager
    {
        private const string SaveFileName = "log_consent.json";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static bool HasDecided()
        {
            return File.Exists(SavePath);
        }

        public static bool HasAgreed()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    return false;
                }

                LogConsentData data = JsonUtility.FromJson<LogConsentData>(File.ReadAllText(SavePath));
                return data != null && data.agreed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SaveDecision(bool agreed)
        {
            LogConsentData data = new LogConsentData
            {
                agreed = agreed,
                consentDateUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }
    }
}
