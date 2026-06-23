using UnityEngine;

namespace Week14.UI
{
    [CreateAssetMenu(fileName = "BossData", menuName = "Week14/Boss Data")]
    public sealed class BossData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string bossName;
        [SerializeField] private string crime;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private string sceneName;

        public string Id => id;
        public string BossName => bossName;
        public string Crime => crime;
        public string Description => description;
        public Sprite Icon => icon;
        public string SceneName => sceneName;
    }
}
