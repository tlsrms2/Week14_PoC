using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;
using Week14.UI;

namespace Week14.Editor
{
    public static class EnemyStatusPrefabBuilder
    {
        [MenuItem("Week14/Combat/Build Selected Enemy Status UI")]
        public static void BuildSelectedEnemyStatusUi()
        {
            foreach (Object selectedObject in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObject);
                if (string.IsNullOrEmpty(path) || PrefabUtility.GetPrefabAssetType(selectedObject) == PrefabAssetType.NotAPrefab)
                {
                    continue;
                }

                BuildPrefab(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            EnemyAI enemy = root.GetComponentInChildren<EnemyAI>();
            Health durability = root.GetComponentInChildren<Health>();
            HeatGauge heat = root.GetComponentInChildren<HeatGauge>();

            if (enemy == null || durability == null || heat == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning($"{path} requires {nameof(EnemyAI)}, {nameof(Health)}, {nameof(HeatGauge)}.");
                return;
            }

            EnemyStatusView statusView = root.GetComponentInChildren<EnemyStatusView>();
            if (statusView == null)
            {
                statusView = enemy.gameObject.AddComponent<EnemyStatusView>();
            }

            EnemyData data = GetEnemyData(enemy);
            statusView.Configure(data);
            statusView.SetTargets(durability, heat);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static EnemyData GetEnemyData(EnemyAI enemy)
        {
            SerializedObject serializedObject = new SerializedObject(enemy);
            SerializedProperty dataProperty = serializedObject.FindProperty("data");
            return dataProperty != null ? dataProperty.objectReferenceValue as EnemyData : null;
        }
    }
}
