using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Editor
{
    public static class CombatProjectilePrefabBuilder
    {
        private const string PrefabRoot = "Assets/Prefabs";
        private const string PlayerBulletPath = "Assets/Prefabs/Combat/PlayerBullet.prefab";
        private const string EnemyBulletPath = "Assets/Prefabs/Combat/EnemyBullet.prefab";

        [MenuItem("Week14/Combat/Create Projectile Prefabs")]
        public static void CreateProjectilePrefabs()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabRoot, "Combat");

            PlayerProjectile playerProjectile = LoadOrCreatePrefab<PlayerProjectile>(
                PlayerBulletPath,
                "PlayerBullet",
                new Color(1f, 0.35f, 0.12f, 1f),
                0.08f,
                21);

            EnemyProjectile enemyProjectile = LoadOrCreatePrefab<EnemyProjectile>(
                EnemyBulletPath,
                "EnemyBullet",
                new Color(1f, 0.95f, 0.25f, 1f),
                0.12f,
                20);

            AssignSelectedConfigs(playerProjectile, enemyProjectile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static T LoadOrCreatePrefab<T>(string path, string name, Color color, float radius, int sortingOrder)
            where T : Component
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null)
            {
                return RepairPrefab<T>(path, color, radius, sortingOrder);
            }

            GameObject projectileObject = new GameObject(name);

            SpriteRenderer spriteRenderer = projectileObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;

            Rigidbody2D rigidbody = projectileObject.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = radius;

            projectileObject.AddComponent<T>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectileObject, path);
            Object.DestroyImmediate(projectileObject);
            return prefab.GetComponent<T>();
        }

        private static T RepairPrefab<T>(string path, Color color, float radius, int sortingOrder)
            where T : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = root.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;

            Rigidbody2D rigidbody = root.GetComponent<Rigidbody2D>();
            if (rigidbody == null)
            {
                rigidbody = root.AddComponent<Rigidbody2D>();
            }

            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = root.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = radius;

            if (root.GetComponent<T>() == null)
            {
                root.AddComponent<T>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<T>();
        }

        private static void AssignSelectedConfigs(PlayerProjectile playerProjectile, EnemyProjectile enemyProjectile)
        {
            foreach (Object selectedObject in Selection.objects)
            {
                if (selectedObject is PlayerCombatConfig)
                {
                    AssignProjectilePrefab(selectedObject, playerProjectile);
                }
                else if (selectedObject is EnemyData)
                {
                    AssignProjectilePrefab(selectedObject, enemyProjectile);
                }
            }
        }

        private static void AssignProjectilePrefab(Object config, Object projectilePrefab)
        {
            SerializedObject serializedObject = new SerializedObject(config);
            SerializedProperty projectileProperty = serializedObject.FindProperty("projectilePrefab");
            if (projectileProperty == null)
            {
                return;
            }

            projectileProperty.objectReferenceValue = projectilePrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
