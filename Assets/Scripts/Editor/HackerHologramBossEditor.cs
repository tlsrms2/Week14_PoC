using UnityEditor;
using Week14.Enemy;

[CustomEditor(typeof(HackerHologramBoss))]
internal sealed class HackerHologramBossEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "홀로그램은 본체의 기록된 움직임을 재생하면서 동일 액션의 공격 판정과 패링 창을 실행합니다. "
            + "상태 UI, 처형 대상, 데미지 표시와 본체 Collider는 제거됩니다. "
            + "Health, BulletGauge, Rigidbody2D는 액션 실행 호스트용으로만 유지됩니다.",
            MessageType.Info);
    }
}
