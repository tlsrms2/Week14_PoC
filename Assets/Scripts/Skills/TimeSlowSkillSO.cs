using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Time Slow Skill", fileName = "TimeSlowSkill")]
    public sealed class TimeSlowSkillSO : BaseSkillSO
    {
        [Tooltip("적/적탄에게 적용할 속도 배율입니다. 0.2 = 80% 감소.")]
        [SerializeField, Range(0f, 1f)] private float slowMultiplier = 0.2f;
        [Tooltip("느려진 상태가 유지되는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float durationSeconds = 5f;
        [Tooltip("스킬 발동 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [SerializeField] private string sfxId;

        [Header("VFX")]
        [Tooltip("효과가 지속되는 동안 잔상이 생성되는 간격(초)입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageInterval = 0.06f;
        [Tooltip("잔상 하나가 사라지는 데 걸리는 시간(초)입니다. 0이면 잔상을 끕니다.")]
        [SerializeField, Min(0f)] private float afterimageDuration = 0.35f;
        [Tooltip("무지개색이 한 바퀴(색상환 360도) 도는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.05f)] private float rainbowCycleSeconds = 1.2f;
        [Tooltip("잔상 색상의 채도입니다.")]
        [SerializeField, Range(0f, 1f)] private float rainbowSaturation = 0.85f;
        [Tooltip("잔상 색상의 명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float rainbowValue = 1f;
        [Tooltip("잔상 색상의 알파(투명도)입니다.")]
        [SerializeField, Range(0f, 1f)] private float rainbowAlpha = 0.45f;
        [Tooltip("이 속도(유닛/초) 이상으로 움직이고 있을 때만 잔상을 생성합니다.")]
        [SerializeField, Min(0f)] private float minSpeedForAfterimage = 0.1f;

        [Header("Screen FX")]
        [Tooltip("화면 전체에 씌울 색상입니다. 알파는 필터 강도(원본 색과의 블렌드 비율)로 사용됩니다.")]
        [SerializeField] private Color screenTintColor = new Color(0f, 1f, 0.3f, 0.35f);
        [Tooltip("필터가 서서히 나타나는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float screenTintFadeIn = 0.15f;
        [Tooltip("필터가 서서히 사라지는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float screenTintFadeOut = 0.25f;

        private Coroutine afterimageRoutine;
        private readonly List<GameObject> activeAfterimages = new List<GameObject>();

        public override bool HasDelayedCooldownStart => true;

        public override void SubscribeEffectEnd(Action onEffectEnd) => EnemyTimeScale.Expired += onEffectEnd;
        public override void UnsubscribeEffectEnd(Action onEffectEnd) => EnemyTimeScale.Expired -= onEffectEnd;

        public override void Execute(GameObject user)
        {
            EnemyTimeScale.SetTemporary(slowMultiplier, durationSeconds);
            TimeSlowScreenFx.Show(durationSeconds, screenTintFadeIn, screenTintFadeOut, screenTintColor);

            if (!string.IsNullOrEmpty(sfxId))
            {
                SoundManager.PlaySfx(sfxId);
            }

            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller != null && afterimageDuration > 0f)
            {
                if (afterimageRoutine != null)
                {
                    controller.StopCoroutine(afterimageRoutine);
                }

                ClearActiveAfterimages();
                afterimageRoutine = controller.StartCoroutine(RainbowAfterimageRoutine(controller, durationSeconds));
            }
        }

        private IEnumerator RainbowAfterimageRoutine(PlayerCombatController controller, float seconds)
        {
            WaitForSeconds wait = new WaitForSeconds(afterimageInterval);
            float startTime = Time.time;
            float minSpeedSqr = minSpeedForAfterimage * minSpeedForAfterimage;

            while (Time.time - startTime < seconds)
            {
                Rigidbody2D body = controller.Context.Body;
                if (body != null && body.linearVelocity.sqrMagnitude >= minSpeedSqr)
                {
                    float hue = Mathf.Repeat(Time.time / rainbowCycleSeconds, 1f);
                    Color tint = Color.HSVToRGB(hue, rainbowSaturation, rainbowValue);
                    tint.a = rainbowAlpha;

                    GameObject spawned = PlayerDashVfx.SpawnRollAfterimage(
                        controller,
                        controller.Context.BodyRenderers,
                        controller.Context.BodyBaseColors,
                        afterimageDuration,
                        tint);

                    if (spawned != null)
                    {
                        activeAfterimages.Add(spawned);
                    }
                }

                yield return wait;
            }

            ClearActiveAfterimages();
            afterimageRoutine = null;
        }

        private void ClearActiveAfterimages()
        {
            for (int i = 0; i < activeAfterimages.Count; i++)
            {
                if (activeAfterimages[i] != null)
                {
                    UnityEngine.Object.Destroy(activeAfterimages[i]);
                }
            }

            activeAfterimages.Clear();
        }
    }
}
