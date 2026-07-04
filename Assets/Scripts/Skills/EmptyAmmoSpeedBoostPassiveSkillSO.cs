using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Empty Ammo Speed Boost", fileName = "EmptyAmmoSpeedBoostPassiveSkill")]
    public sealed class EmptyAmmoSpeedBoostPassiveSkillSO : BasePassiveSkillSO
    {
        [Tooltip("보유 탄환이 0일 때 적용할 이동 속도 배율입니다. 1.25 = 25% 증가.")]
        [SerializeField, Min(0f)] private float emptyAmmoMoveSpeedMultiplier = 1.25f;

        private PlayerCombatController subscribedController;
        private BulletGauge subscribedBullets;

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
        }
    }
}
