using System;
using System.Collections;
using UnityEngine;
using Week14.Challenge;

namespace Week14.UI
{
    public sealed class BossChallengePanel : MonoBehaviour
    {
        [Tooltip("챌린지 정의를 조회할 데이터베이스입니다.")]
        [SerializeField] private ChallengeDatabaseSO database;
        [Tooltip("보스의 챌린지를 순서대로 표시할 슬롯입니다. 챌린지 수가 이보다 적으면 남는 슬롯은 비웁니다.")]
        [SerializeField] private ChallengeSlotView[] slots = Array.Empty<ChallengeSlotView>();
        [Tooltip("챌린지를 아직 클리어하지 않았을 때 슬롯 이미지에 표시할 스프라이트입니다.")]
        [SerializeField] private Sprite incompleteSprite;
        [Tooltip("챌린지를 클리어했을 때 슬롯 이미지에 표시할 스프라이트입니다.")]
        [SerializeField] private Sprite completedSprite;

        [Header("결과 화면 공개 연출 (PlayReveal)")]
        [Tooltip("연출 시작 전 대기 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float initialDelaySeconds = 0.3f;
        [Tooltip("스윕이 도달하기 전까지의 기본 텍스트 색상입니다.")]
        [SerializeField] private Color defaultTextColor = Color.white;
        [Tooltip("챌린지를 클리어했을 때의 텍스트 색상입니다. Show()(로비 호버)에서도 사용됩니다.")]
        [SerializeField] private Color clearedTextColor = new(0.4f, 1f, 0.5f);
        [Tooltip("챌린지를 클리어하지 못했을 때의 텍스트 색상입니다.")]
        [SerializeField] private Color notClearedTextColor = new(1f, 0.4f, 0.4f);
        [Tooltip("스윕 이미지가 최대로 커졌을 때의 Width입니다.")]
        [SerializeField, Min(0f)] private float sweepWidth = 200f;
        [Tooltip("스윕 이미지가 0에서 최대 Width까지 커지는 데 걸리는 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float sweepGrowSeconds = 0.12f;
        [Tooltip("스윕 이미지가 최대 Width에서 다시 0으로 줄어드는 데 걸리는 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float sweepShrinkSeconds = 0.12f;

        private Coroutine revealRoutine;

        public void Show(BossData bossData)
        {
            StopRevealRoutine();

            if (database == null || bossData == null)
            {
                ClearAll();
                return;
            }

            string bossId = bossData.Id;
            int index = 0;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(bossId))
            {
                if (index >= slots.Length)
                {
                    break;
                }

                if (slots[index] != null)
                {
                    slots[index].Show(definition, bossId, completedSprite, incompleteSprite, clearedTextColor);
                }

                index++;
            }

            for (; index < slots.Length; index++)
            {
                if (slots[index] != null)
                {
                    slots[index].Clear();
                }
            }
        }

        public void PlayReveal(BossData bossData)
        {
            StopRevealRoutine();
            ClearAll();

            if (database == null || bossData == null)
            {
                return;
            }

            revealRoutine = StartCoroutine(PlayRevealRoutine(bossData));
        }

        public void Hide()
        {
            StopRevealRoutine();
            ClearAll();
        }

        private IEnumerator PlayRevealRoutine(BossData bossData)
        {
            if (initialDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(initialDelaySeconds);
            }

            string bossId = bossData.Id;
            int index = 0;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(bossId))
            {
                if (index >= slots.Length)
                {
                    break;
                }

                ChallengeSlotView slot = slots[index];
                if (slot != null)
                {
                    yield return slot.PlayRevealCoroutine(
                        definition, bossId, completedSprite, incompleteSprite,
                        defaultTextColor, clearedTextColor, notClearedTextColor,
                        sweepWidth, sweepGrowSeconds, sweepShrinkSeconds);
                }

                index++;
            }

            revealRoutine = null;
        }

        private void StopRevealRoutine()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
        }

        private void ClearAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].Clear();
                }
            }
        }
    }
}
