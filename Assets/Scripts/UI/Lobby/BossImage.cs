using System.Collections.Generic;
using UnityEngine;
using Week14.Challenge;
using Week14.Save;

namespace Week14.UI
{
    // BossSlot(보스 패널의 슬롯)과는 별개로, 보스 한 명을 SpriteRenderer로 크게 보여주는 연출용 컴포넌트입니다.
    // 해금 여부에 따라 오브젝트 자체를 숨기고, 클리어 여부에 따라 보스 이미지를 사망 이미지로 바꾸며,
    // 챌린지 전체 완료 여부에 따라 배경색을 바꿉니다.
    public sealed class BossImage : MonoBehaviour
    {
        [SerializeField] private BossData bossData;
        [Tooltip("\"모든 챌린지 완료\" 판정에 쓸, 이 보스의 챌린지 목록을 조회할 데이터베이스입니다.")]
        [SerializeField] private ChallengeDatabaseSO database;

        [Header("스프라이트 렌더러")]
        [Tooltip("배경 스프라이트를 표시할 렌더러입니다.")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [Tooltip("보스 아이콘/사망 이미지를 표시할 렌더러입니다.")]
        [SerializeField] private SpriteRenderer bossRenderer;
        [Tooltip("보스를 클리어했을 때 함께 활성화할 렌더러 목록입니다. 클리어 전에는 모두 꺼둡니다.")]
        [SerializeField] private List<SpriteRenderer> clearedRenderers = new();

        [Header("배경 색")]
        [Tooltip("기본(챌린지 미완료) 상태의 배경색입니다.")]
        [SerializeField] private Color defaultBackgroundColor = Color.white;
        [Tooltip("이 보스의 챌린지를 모두 완료했을 때 배경에 적용할 색입니다.")]
        [SerializeField] private Color challengeCompleteColor = Color.white;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (bossData == null || !bossData.IsUnlocked())
            {
                gameObject.SetActive(false);
                return;
            }

            bool cleared = GameSaveManager.IsCleared(bossData.Id);

            if (bossRenderer != null)
            {
                bossRenderer.sprite = cleared ? bossData.ResultPortrait : bossData.Icon;
            }

            SetClearedRenderersActive(cleared);

            if (backgroundRenderer != null)
            {
                backgroundRenderer.color = AreAllChallengesCompleted(bossData.Id) ? challengeCompleteColor : defaultBackgroundColor;
            }
        }

        private void SetClearedRenderersActive(bool active)
        {
            for (int i = 0; i < clearedRenderers.Count; i++)
            {
                SpriteRenderer clearedRenderer = clearedRenderers[i];
                if (clearedRenderer != null)
                {
                    clearedRenderer.enabled = active;
                }
            }
        }

        private bool AreAllChallengesCompleted(string bossId)
        {
            if (database == null)
            {
                return false;
            }

            bool hasAny = false;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(bossId))
            {
                hasAny = true;
                string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId);
                if (!GameSaveManager.IsChallengeCompleted(saveKey))
                {
                    return false;
                }
            }

            return hasAny;
        }
    }
}
