using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    public enum HackerRecallWeaponSelection
    {
        Bayonet,
        Gun,
        Sword,
        RandomAvailable
    }

    [Serializable]
    public sealed class HackerRecallWeaponAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField] private HackerRecallWeaponSelection weaponSelection = HackerRecallWeaponSelection.RandomAvailable;
        [SerializeField, BossGraphBossChildPath] private string returnAnchorPath;
        [SerializeField] private string animationTriggerName = "RecallWeapon";
        [SerializeField, Min(0f)] private float windupSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float recallSeconds = 0.55f;
        [SerializeField] private string weaponWireAnchorPath = "WireAnchor";
        [SerializeField] private float recallRotationDegrees = 360f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;
        [SerializeField, BossGraphSfxId] private string wireSfxId = HackerSfxIds.Wire;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker
                || !TryGetWeapon(hacker, out HackerThrownWeapon weapon)
                || weapon == null)
            {
                yield break;
            }

            Transform returnAnchor = context.GetBossChildTransform(returnAnchorPath) ?? hacker.transform;
            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);
            Transform weaponWireAnchor = weapon.GetChildTransform(weaponWireAnchorPath) ?? weapon.transform;
            if (!weapon.BeginRecall(returnAnchor, weaponWireAnchor, recallSeconds, recallRotationDegrees))
            {
                yield break;
            }

            HackerWireSettings wireSettings = hacker.WireSettings;
            context.PlaySfx(HackerSfxIds.Resolve(wireSfxId, HackerSfxIds.Wire));
            HackerRecallWireVisual.Create(
                returnAnchor,
                weaponWireAnchor,
                wireSettings.Color,
                wireSettings.Width);
            yield return HackerMeleeAttackAction.Wait(context, recallSeconds);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0.05f, recallSeconds) + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private bool TryGetWeapon(HackerBossAI hacker, out HackerThrownWeapon weapon)
        {
            if (weaponSelection != HackerRecallWeaponSelection.RandomAvailable)
            {
                return hacker.TryGetGroundedWeapon((HackerThrownWeaponType)weaponSelection, out weapon);
            }

            HackerThrownWeaponType[] types =
            {
                HackerThrownWeaponType.ThrowingWeapon,
                HackerThrownWeaponType.Bayonet,
                HackerThrownWeaponType.Gun,
                HackerThrownWeaponType.Sword
            };
            int firstIndex = UnityEngine.Random.Range(0, types.Length);
            for (int i = 0; i < types.Length; i++)
            {
                HackerThrownWeaponType type = types[(firstIndex + i) % types.Length];
                if (hacker.TryGetGroundedWeapon(type, out weapon))
                {
                    return true;
                }
            }

            weapon = null;
            return false;
        }
    }

    internal sealed class HackerRecallWireVisual : MonoBehaviour
    {
        private Transform start;
        private Transform end;
        private LineRenderer line;

        internal static HackerRecallWireVisual Create(Transform start, Transform end, Color color, float width)
        {
            GameObject visualObject = new("HackerRecallWireVisual");
            HackerRecallWireVisual visual = visualObject.AddComponent<HackerRecallWireVisual>();
            visual.start = start;
            visual.end = end;
            visual.CreateLine(color, width);
            return visual;
        }

        private void LateUpdate()
        {
            if (start == null || end == null)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            line.SetPosition(0, start.position);
            line.SetPosition(1, end.position);
        }

        private void CreateLine(Color color, float width)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = Mathf.Max(0.01f, width);
            line.endWidth = Mathf.Max(0.01f, width);
            line.startColor = color;
            line.endColor = color;
            BossSorting.Apply(line);
            line.sortingOrder = 20;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }
        }
    }
}
