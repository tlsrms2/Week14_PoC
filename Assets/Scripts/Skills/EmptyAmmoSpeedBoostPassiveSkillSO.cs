using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Empty Ammo Speed Boost", fileName = "EmptyAmmoSpeedBoostPassiveSkill")]
    public sealed class EmptyAmmoSpeedBoostPassiveSkillSO : BasePassiveSkillSO
    {
        [Tooltip("보유 탄환이 0일 때 적용할 이동 속도 배율입니다. 1.25 = 25% 증가.")]
        [SerializeField, Min(0f)] private float emptyAmmoMoveSpeedMultiplier = 1.25f;

        [Header("VFX")]
        [Tooltip("탄환이 비어 속도 부스트 중일 때 잔상이 생성되는 간격(초)입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageInterval = 0.08f;
        [Tooltip("잔상이 사라지는 데 걸리는 시간(초)입니다. 0이면 잔상을 끕니다.")]
        [SerializeField, Min(0f)] private float afterimageDuration = 0.2f;
        [Tooltip("잔상 색상입니다. 알파로 투명도를 조절합니다.")]
        [SerializeField] private Color afterimageColor = new Color(1f, 0.85f, 0.35f, 0.35f);
        [Tooltip("이 속도(유닛/초) 이상으로 움직이고 있을 때만 잔상을 생성합니다.")]
        [SerializeField, Min(0f)] private float minSpeedForAfterimage = 0.1f;

        private PlayerCombatController subscribedController;
        private BulletGauge subscribedBullets;
        private Coroutine afterimageRoutine;

        public override void ApplyPassive(GameObject player)
        {
            Unsubscribe();

            PlayerCombatController controller = ResolvePlayerController(player);
            if (controller == null || controller.Bullets == null)
            {
                return;
            }

            subscribedController = controller;
            subscribedBullets = controller.Bullets;
            subscribedBullets.Changed += HandleAmmoChanged;
            RefreshMultiplier();
        }

        public override void RemovePassive(GameObject player)
        {
            subscribedController?.SetMoveSpeedMultiplier(1f);
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            SetAfterimageActive(false);

            if (subscribedBullets != null)
            {
                subscribedBullets.Changed -= HandleAmmoChanged;
            }

            subscribedBullets = null;
            subscribedController = null;
        }

        private void HandleAmmoChanged(int current, int max)
        {
            RefreshMultiplier();
        }

        private void RefreshMultiplier()
        {
            if (subscribedController == null)
            {
                return;
            }

            bool isEmpty = subscribedBullets != null && subscribedBullets.IsEmpty;
            subscribedController.SetMoveSpeedMultiplier(isEmpty ? emptyAmmoMoveSpeedMultiplier : 1f);
            SetAfterimageActive(isEmpty);
        }

        private void SetAfterimageActive(bool active)
        {
            if (active)
            {
                if (afterimageRoutine == null && subscribedController != null && afterimageDuration > 0f)
                {
                    afterimageRoutine = subscribedController.StartCoroutine(AfterimageRoutine(subscribedController));
                }

                return;
            }

            if (afterimageRoutine != null)
            {
                if (subscribedController != null)
                {
                    subscribedController.StopCoroutine(afterimageRoutine);
                }

                afterimageRoutine = null;
            }
        }

        private IEnumerator AfterimageRoutine(PlayerCombatController controller)
        {
            float minSpeedSqr = minSpeedForAfterimage * minSpeedForAfterimage;
            WaitForSeconds wait = new WaitForSeconds(afterimageInterval);

            while (true)
            {
                Rigidbody2D body = controller.Context.Body;
                if (body != null && body.linearVelocity.sqrMagnitude >= minSpeedSqr)
                {
                    PlayerDashVfx.SpawnRollAfterimage(
                        controller,
                        controller.Context.BodyRenderers,
                        controller.Context.BodyBaseColors,
                        afterimageDuration,
                        afterimageColor);
                }

                yield return wait;
            }
        }
    }
}
