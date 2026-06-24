using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Boss/Boss Color Settings", fileName = "BossColorSettings")]
    public sealed class BossColorSettings : ScriptableObject
    {
        [Header("Color")]
        [Tooltip("기본 상태에서 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("보스 HP가 0이 되었을 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color hpEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("보스가 경직 상태일 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Status UI")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [FormerlySerializedAs("bulletBarColor")]
        [SerializeField] private Color hpBarColor = new(1f, 0.55f, 0.1f, 1f);
        [FormerlySerializedAs("emptyBulletBarColor")]
        [SerializeField] private Color emptyHpBarColor = Color.red;
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [SerializeField] private Color executionIndicatorColor = Color.red;

        public Color NormalColor => normalColor;
        public Color HpEmptyColor => hpEmptyColor;
        public Color StaggeredColor => staggeredColor;
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color HpBarColor => hpBarColor;
        public Color EmptyHpBarColor => emptyHpBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
    }
}
