using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

[CustomEditor(typeof(BossGraphActionAsset), true)]
public sealed class BossGraphActionAssetEditor : Editor
{
    private SerializedProperty actions;

    private void OnEnable()
    {
        actions = serializedObject.FindProperty("actions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TrimToSingleAction();
        DrawSingleAction();
        serializedObject.ApplyModifiedProperties();
    }

    private void TrimToSingleAction()
    {
        if (actions == null || actions.arraySize <= 1)
        {
            return;
        }

        Undo.RecordObject(target, "Trim Boss Graph Action");
        while (actions.arraySize > 1)
        {
            actions.DeleteArrayElementAtIndex(actions.arraySize - 1);
        }

        EditorUtility.SetDirty(target);
    }

    private void DrawSingleAction()
    {
        if (actions == null || actions.arraySize == 0)
        {
            return;
        }

        SerializedProperty action = actions.GetArrayElementAtIndex(0);
        Type actionType = action.managedReferenceValue?.GetType();
        string description = BossGraphActionEditorUtility.GetActionDescription(actionType);
        if (!BossGraphDrawerDescriptionGui.SuppressDescriptions && !string.IsNullOrWhiteSpace(description))
        {
            EditorGUILayout.HelpBox(description, MessageType.Info);
        }

        EditorGUILayout.PropertyField(action, new GUIContent(GetActionLabel(action)), true);
    }

    private static string GetActionLabel(SerializedProperty action)
    {
        object value = action.managedReferenceValue;
        return value == null
            ? "Action"
            : ObjectNames.NicifyVariableName(value.GetType().Name);
    }
}

internal readonly struct BossGraphActionMenuItem
{
    public BossGraphActionMenuItem(string menuPath, Type actionType, Func<BossAction> create)
    {
        MenuPath = menuPath;
        ActionType = actionType;
        Create = create;
    }

    public string MenuPath { get; }
    public Type ActionType { get; }
    public Func<BossAction> Create { get; }
}

internal static class BossGraphActionEditorUtility
{
    public static readonly IReadOnlyList<BossGraphActionMenuItem> ActionMenuItems = new List<BossGraphActionMenuItem>
    {
        new("Utility/Wait", typeof(WaitAction), () => new WaitAction()),
        new("Utility/Windup", typeof(WindupAction), () => new WindupAction()),
        new("Animation/Set Parameter", typeof(PlayAnimationAction), () => new PlayAnimationAction()),
        new("Animation/Wait For Event", typeof(WaitForAnimationEventAction), () => new WaitForAnimationEventAction()),
        new("Move/Move Toward Player", typeof(MoveTowardPlayerAction), () => new MoveTowardPlayerAction()),
        new("Move/Maintain Player Distance", typeof(MaintainPlayerDistanceAction), () => new MaintainPlayerDistanceAction()),
        new("Move/Move Until Player Distance", typeof(MoveUntilPlayerDistanceAction), () => new MoveUntilPlayerDistanceAction()),
        new("Move/Start Move Toward Player", typeof(StartMoveTowardPlayerAction), () => new StartMoveTowardPlayerAction()),
        new("Move/Start Move Away From Player", typeof(StartMoveAwayFromPlayerAction), () => new StartMoveAwayFromPlayerAction()),
        new("Move/Stop Movement", typeof(StopMovementAction), () => new StopMovementAction()),
        new("Move/Move Body Root", typeof(MoveBodyRootLocalAction), () => new MoveBodyRootLocalAction()),
        new("Move/Stop Body Root", typeof(ResetBodyRootLocalAction), () => new ResetBodyRootLocalAction()),
        new("Move/Boss Dash", typeof(BossDashAction), () => new BossDashAction()),
        new("Move/Wander Around Player Distance", typeof(WanderAroundPlayerDistanceAction), () => new WanderAroundPlayerDistanceAction()),
        new("Move/Move Between Map Points", typeof(MoveBetweenMapPointsAction), () => new MoveBetweenMapPointsAction()),
        new("Move/Random Direction Move", typeof(BossRandomDirectionMoveAction), () => new BossRandomDirectionMoveAction()),
        new("Projectile/Fire Projectile", typeof(FireProjectileAction), () => new FireProjectileAction()),
        new("Projectile/Charged/Spawn Charged Projectile", typeof(SpawnChargedProjectileAction), () => new SpawnChargedProjectileAction()),
        new("Projectile/Charged/Configure Projectile Growth", typeof(ConfigureProjectileGrowthAction), () => new ConfigureProjectileGrowthAction()),
        new("Projectile/Charged/Configure Radial Split", typeof(ConfigureRadialSplitAction), () => new ConfigureRadialSplitAction()),
        new("Projectile/Charged/Wait Projectile Charge End", typeof(WaitProjectileChargeEndAction), () => new WaitProjectileChargeEndAction()),
        new("Projectile/Fire Projectile Burst", typeof(FireProjectileBurstAction), () => new FireProjectileBurstAction()),
        new("Projectile/Fire Random Cone Burst", typeof(FireRandomConeBurstAction), () => new FireRandomConeBurstAction()),
        new("Projectile/Fire Player Side Fan Sweep", typeof(FirePlayerSideFanSweepAction), () => new FirePlayerSideFanSweepAction()),
        new("Projectile/Fire Radial Emission", typeof(FireRadialEmissionAction), () => new FireRadialEmissionAction()),
        new("Projectile/Fire Sweep Emission", typeof(FireSweepEmissionAction), () => new FireSweepEmissionAction()),
        new("Projectile/Fire Fan Emission", typeof(FireFanEmissionAction), () => new FireFanEmissionAction()),
        new("Projectile/Fire Dash Formation", typeof(FireDashFormationAction), () => new FireDashFormationAction()),
        new("Projectile/Fire Distance Trail", typeof(FireDistanceTrailProjectilesAction), () => new FireDistanceTrailProjectilesAction()),
        new("Projectile/Fire Rotating Spiral", typeof(FireRotatingProjectilesAction), () => new FireRotatingProjectilesAction()),
        new("Projectile/Fire Attached Projectiles", typeof(FireAttachedProjectilesAction), () => new FireAttachedProjectilesAction()),
        new("Projectile/Fire Configured Volley", typeof(FireConfiguredVolleyProjectilesAction), () => new FireConfiguredVolleyProjectilesAction()),
        new("Projectile/Fire Player Circle", typeof(FirePlayerCircleProjectilesAction), () => new FirePlayerCircleProjectilesAction()),
        new("Projectile/Spawn Parryable Bomb", typeof(SpawnParryableBombAction), () => new SpawnParryableBombAction()),
        new("Projectile/Fire Parry Suppression Bait", typeof(FireParrySuppressionBaitAction), () => new FireParrySuppressionBaitAction()),
        new("Combat/Boss Area Damage", typeof(BossAreaDamageAction), () => new BossAreaDamageAction()),
        new("Arsonist/Fire Circle Orbit Attack", typeof(ArsonistCircleOrbitAttackAction), () => new ArsonistCircleOrbitAttackAction()),
        new("Arsonist/Fire Character", typeof(ArsonistFireCharacterProjectileAction), () => new ArsonistFireCharacterProjectileAction()),
        new("Arsonist/Set Sprinkler Active", typeof(ArsonistSetSprinklerActiveAction), () => new ArsonistSetSprinklerActiveAction()),
        new("Assassin/Fire Homing Dagger", typeof(FireAssassinHomingDaggerProjectileAction), () => new FireAssassinHomingDaggerProjectileAction()),
        new("Assassin/Enter Stealth", typeof(AssassinEnterStealthAction), () => new AssassinEnterStealthAction()),
        new("Assassin/Set Stealth Visibility", typeof(AssassinSetStealthVisibilityAction), () => new AssassinSetStealthVisibilityAction()),
        new("Assassin/Recall Daggers", typeof(AssassinRecallDaggersAction), () => new AssassinRecallDaggersAction()),
        new("Assassin/Recall Daggers With Parry Bait", typeof(AssassinRecallDaggersWithParryBaitAction), () => new AssassinRecallDaggersWithParryBaitAction()),
        new("Assassin/Spawn Clone Shooters", typeof(AssassinSpawnCloneShootersAction), () => new AssassinSpawnCloneShootersAction()),
        new("Assassin/Fire Next Clone Shooter", typeof(AssassinFireNextCloneShooterAction), () => new AssassinFireNextCloneShooterAction()),
        new("Assassin/Spawn Random Bombs", typeof(AssassinSpawnRandomBombsAction), () => new AssassinSpawnRandomBombsAction()),
        new("Assassin/Teleport Around Player", typeof(AssassinTeleportAroundPlayerAction), () => new AssassinTeleportAroundPlayerAction()),
        new("Hacker/Melee Attack", typeof(HackerMeleeAttackAction), () => new HackerMeleeAttackAction()),
        new("Hacker/Thrust", typeof(HackerThrustAction), () => new HackerThrustAction()),
        new("Hacker/Thrust Perpendicular Fire", typeof(HackerThrustPerpendicularFireAction), () => new HackerThrustPerpendicularFireAction()),
        new("Hacker/Dash", typeof(HackerDashAction), () => new HackerDashAction()),
        new("Hacker/Charge Dash", typeof(HackerChargeDashAction), () => new HackerChargeDashAction()),
        new("Hacker/Fire Sniping Projectile", typeof(HackerSnipingFireAction), () => new HackerSnipingFireAction()),
        new("Hacker/Fire Sequential Sweep", typeof(HackerSequentialSweepFireAction), () => new HackerSequentialSweepFireAction()),
        new("Hacker/Fire Glitch Projectile", typeof(HackerFireGlitchProjectileAction), () => new HackerFireGlitchProjectileAction()),
        new("Hacker/Throw Weapon", typeof(HackerWeaponThrowAction), () => new HackerWeaponThrowAction()),
        new("Hacker/Dash Sweep", typeof(HackerDashSweepAction), () => new HackerDashSweepAction()),
        new("Hacker/Fire Wire", typeof(HackerFireWireAction), () => new HackerFireWireAction()),
        new("Hacker/Fire Wire Branch", typeof(HackerFireWireBranchAction), () => new HackerFireWireBranchAction()),
        new("Hacker/Hologram Replay", typeof(HackerHologramReplayAction), () => new HackerHologramReplayAction()),
        new("Hacker/Fire Twin Wire", typeof(HackerFireTwinWireAction), () => new HackerFireTwinWireAction()),
        new("Hacker/Ground Gun Turret", typeof(HackerGroundGunTurretAction), () => new HackerGroundGunTurretAction()),
        new("Hacker/Recall Weapon", typeof(HackerRecallWeaponAction), () => new HackerRecallWeaponAction()),
        new("Hacker/Weapon Wire Orbit", typeof(HackerWeaponWireOrbitAction), () => new HackerWeaponWireOrbitAction()),
        new("Hacker/Walk Fire Counter Grab", typeof(HackerWalkFireCounterGrabAction), () => new HackerWalkFireCounterGrabAction()),
        new("Hacker/Scatter Wire Node", typeof(HackerScatterWireNodeAction), () => new HackerScatterWireNodeAction()),
        new("Hacker/Spider Web", typeof(HackerSpiderWebAction), () => new HackerSpiderWebAction()),
        new("Utility/Aim Boss Child At Player", typeof(AimBossChildAtPlayerAction), () => new AimBossChildAtPlayerAction()),
        new("Utility/Custom Event", typeof(CustomEventAction), () => new CustomEventAction()),
        new("Utility/Spawn Prefab", typeof(SpawnPrefabAction), () => new SpawnPrefabAction()),
        new("Utility/Play Sfx", typeof(PlaySfxAction), () => new PlaySfxAction()),
        new("Attack/Conductor/Conducting Cue", typeof(ConductorConductingCueAction), () => new ConductorConductingCueAction()),
        new("Attack/Conductor/Defense Arc Volley", typeof(ConductorDefenseArcVolleyAction), () => new ConductorDefenseArcVolleyAction()),
        new("Attack/Conductor/Diamond Collapse", typeof(ConductorDiamondCollapseAction), () => new ConductorDiamondCollapseAction()),
        new("Attack/Conductor/Parry Suppression Bait", typeof(ConductorParrySuppressionBaitAction), () => new ConductorParrySuppressionBaitAction()),
        new("Minion/Spawn/Summon", typeof(MinionSummonAction), () => new MinionSummonAction()),
        new("Minion/Spawn/Ensure Count", typeof(MinionEnsureCountAction), () => new MinionEnsureCountAction()),
        new("Minion/Spawn/Auto Summon If Needed", typeof(MinionAutoSummonIfNeededAction), () => new MinionAutoSummonIfNeededAction()),
        new("Minion/Fire/Sequential Fire", typeof(MinionSequentialFireAction), () => new MinionSequentialFireAction()),
        new("Minion/Fire/Repeat Fire", typeof(MinionRepeatFireAction), () => new MinionRepeatFireAction()),
        new("Minion/Fire/Radial Burst", typeof(MinionRadialBurstAction), () => new MinionRadialBurstAction()),
        new("Minion/Fire/Side Fire", typeof(MinionSideFireAction), () => new MinionSideFireAction()),
        new("Minion/Movement/Orbit", typeof(MinionOrbitAction), () => new MinionOrbitAction()),
        new("Minion/Movement/Wander", typeof(MinionWanderAction), () => new MinionWanderAction()),
        new("Minion/Movement/Dash", typeof(MinionDashAction), () => new MinionDashAction()),
        new("Minion/Movement/Gather", typeof(MinionGatherAction), () => new MinionGatherAction()),
        new("Minion/Movement/Hold Position", typeof(MinionHoldPositionAction), () => new MinionHoldPositionAction()),
        new("Minion/Movement/Formation Circle", typeof(MinionFormationAction), () => new MinionFormationAction()),
        new("Minion/Movement/Formation Straight", typeof(MinionFormationStraightAction), () => new MinionFormationStraightAction()),
        new("Minion/Movement/Player Path", typeof(MinionPlayerPathAction), () => new MinionPlayerPathAction()),
        new("Minion/Movement/Angle Distance Move", typeof(MinionAngleDistanceMoveAction), () => new MinionAngleDistanceMoveAction()),
        new("Attack/Conductor/Score Lane Rush", typeof(MinionConductorScoreLaneRushAction), () => new MinionConductorScoreLaneRushAction()),
        new("Attack/Conductor/Score Lane Rush Special", typeof(MinionConductorScoreLaneRushSpecialAction), () => new MinionConductorScoreLaneRushSpecialAction()),
        new("Attack/Conductor/Player Path Side Fire", typeof(MinionConductorPlayerPathSideFireAction), () => new MinionConductorPlayerPathSideFireAction()),
        new("Attack/Conductor/Formation Line Volley", typeof(MinionConductorFormationLineVolleyAction), () => new MinionConductorFormationLineVolleyAction()),
        new("Attack/Conductor/Fan Blade", typeof(MinionConductorFanBladeAction), () => new MinionConductorFanBladeAction()),
        new("Attack/Conductor/Spawn Turrets", typeof(ConductorSpawnTurretsAction), () => new ConductorSpawnTurretsAction()),
        new("Minion/Control/Pattern Cleanup", typeof(MinionPatternCleanupAction), () => new MinionPatternCleanupAction())
    };

    public static string GetActionLabel(Type actionType)
    {
        if (actionType == null)
        {
            return "Unknown Action";
        }

        for (int i = 0; i < ActionMenuItems.Count; i++)
        {
            BossGraphActionMenuItem item = ActionMenuItems[i];
            if (item.ActionType == actionType)
            {
                return item.MenuPath;
            }
        }

        return ObjectNames.NicifyVariableName(actionType.Name);
    }

    public static string GetActionDescription(Type actionType)
    {
        if (actionType == typeof(WaitAction))
        {
            return "지정 시간 동안 대기합니다. 지속 이동이 켜져 있으면 대기 중에도 이동 갱신이 유지됩니다.";
        }

        if (actionType == typeof(WindupAction))
        {
            return "공격 전 준비 시간을 담당합니다. 준비 중 이동 정지와 연기 이펙트를 처리할 수 있습니다.";
        }

        if (actionType == typeof(PlayAnimationAction))
        {
            return "Animator 파라미터(Float, Int, Bool, Trigger)를 설정합니다.";
        }

        if (actionType == typeof(WaitForAnimationEventAction))
        {
            return "지정한 애니메이션 이벤트 ID가 들어올 때까지 대기합니다.";
        }

        if (actionType == typeof(MoveTowardPlayerAction))
        {
            return "지정 시간 동안 플레이어 방향으로 이동합니다.";
        }

        if (actionType == typeof(MaintainPlayerDistanceAction))
        {
            return "지정 시간 동안 플레이어와 목표 거리를 유지합니다. 멀면 접근하고 가까우면 후퇴합니다.";
        }

        if (actionType == typeof(MoveUntilPlayerDistanceAction))
        {
            return "플레이어와의 거리가 목표 거리 이하가 될 때까지 접근합니다. 도달하면(또는 타임아웃되면) 멈추고 다음으로 넘어갑니다.";
        }

        if (actionType == typeof(StartMoveTowardPlayerAction))
        {
            return "플레이어 방향 이동을 켭니다. Duration Seconds가 0이면 Stop Movement나 패턴 종료까지 유지됩니다.";
        }

        if (actionType == typeof(StartMoveAwayFromPlayerAction))
        {
            return "플레이어 반대 방향 이동을 켭니다. Duration Seconds가 0이면 Stop Movement나 패턴 종료까지 유지됩니다.";
        }

        if (actionType == typeof(StopMovementAction))
        {
            return "Start Move 계열 액션으로 켠 지속 이동을 끕니다.";
        }

        if (actionType == typeof(MoveBodyRootLocalAction))
        {
            return "보스 본체를 패턴 시작 시점 위치 기준 상대 오프셋만큼 이동시킵니다(절대 맵 좌표 아님).";
        }

        if (actionType == typeof(ResetBodyRootLocalAction))
        {
            return "보스 본체 이동을 정지합니다.";
        }

        if (actionType == typeof(FireProjectileAction))
        {
            return "단발 투사체를 발사합니다. 간단한 1발 발사용 액션입니다.";
        }

        if (actionType == typeof(FireRotatingProjectilesAction))
        {
            return "보스 위치를 시작점으로 투사체가 모기향처럼 회전하며 바깥으로 이동합니다. Ring Count와 Ring Spacing으로 같은 모기향 사이에 추가 고리를 만들 수 있습니다.";
        }

        if (actionType == typeof(FireAttachedProjectilesAction))
        {
            return "투사체를 보스 위치에 붙여 보스와 함께 움직이게 합니다. 반경과 회전 속도를 설정할 수 있습니다.";
        }

        if (actionType == typeof(FireConfiguredVolleyProjectilesAction))
        {
            return "Volley마다 투사체 종류, 각도 오프셋, 발사 간격, 휴식 시간을 따로 설정해 발사합니다.";
        }

        if (actionType == typeof(FirePlayerCircleProjectilesAction))
        {
            return "패턴 시작 시점의 플레이어 위치를 중심으로 투사체가 원을 그리며 이동합니다.";
        }

        if (actionType == typeof(ArsonistCircleOrbitAttackAction))
        {
            return "Arsonist 전용 액션입니다. 플레이어 위치를 중심으로 기름 원을 깔고, 보스가 같은 원주를 돌며 설정한 발리를 발사합니다.";
        }

        if (actionType == typeof(ArsonistFireCharacterProjectileAction))
        {
            return "Arsonist 전용 액션입니다. 火자의 네 획 시작점에서 투사체를 순서대로 생성해 획을 따라 이동시킵니다.";
        }

        if (actionType == typeof(ArsonistSetSprinklerActiveAction))
        {
            return "Arsonist 보스 인스펙터의 Sprinklers 리스트에서 지정 인덱스의 스프링쿨러 기능 활성 상태를 바꿉니다. 사용된 스프링쿨러는 다시 활성화되지 않습니다.";
        }

        if (actionType == typeof(FireAssassinHomingDaggerProjectileAction))
        {
            return "Assassin 전용 액션입니다. 유도탄을 발사하고, 플레이어에게 패링당하면(Intercepted) 그 위치에 단검을 스폰합니다. 은신 그래프에서만 사용하세요.";
        }

        if (actionType == typeof(AssassinEnterStealthAction))
        {
            return "Assassin 전용 액션입니다. 실행 시 은신 상태로 전환합니다(통상 그래프 → 은신 그래프로 다음 틱에 전환).";
        }

        if (actionType == typeof(AssassinSetStealthVisibilityAction))
        {
            return "Assassin 전용 액션입니다. 은신 상태(isStealthed)는 그대로 둔 채, 보스 스프라이트만 시각적으로 완전히 보이게(Visible 체크) 또는 다시 은신 알파값으로(체크 해제) 되돌립니다. 회수 패턴에서 패링 미끼를 스폰하기 전에 보스를 잠깐 노출시키는 용도로 씁니다.";
        }

        if (actionType == typeof(AssassinRecallDaggersAction))
        {
            return "Assassin 전용 액션입니다. 스폰된 단검 개수가 충분하면 전부 보스에게 회수하며 비행 중 플레이어에게 데미지를 주고, 끝나면 은신을 해제합니다. 단검이 부족하면 즉시 종료됩니다(이 패턴의 Cooldown Pattern Count는 0으로 설정하세요).";
        }

        if (actionType == typeof(AssassinRecallDaggersWithParryBaitAction))
        {
            return "Assassin 전용 액션입니다. 단검이 충분히 모였을 때만 동작하며, 회수 전에 패링 전용 미끼(ParryBaitRewardProjectile)를 먼저 스폰합니다. 패링되지 않으면 단검이 보스에게 날아가며(궤적 표시, 닿는 플레이어에게 데미지) 회수되고 뒤에 있는 패턴이 그대로 이어집니다. 패링되면 그 자리에 보상 탄이 원형으로 뿌려지고 단검이 궤적 없이 보스 자신에게 회수되어(자해 데미지) 회수가 끝난 뒤 FireParrySuppressionBaitAction(그로기탄)과 완전히 동일하게 boss.RequestGroggy(Groggy Seconds)로 패턴을 취소하고 보스가 그로기(무력화, Stun/EndStun 애니메이터)에 들어갑니다. 은신 해제는 회수가 끝나는 시점(RecallAllDaggersRoutine 종료)에 자동으로 처리됩니다. 보상 탄 프리팹은 미끼 프리팹 자신의 Reward Projectile 필드에 지정하고, 개수/반지름/지속시간은 이 액션의 필드로 덮어씁니다. 궤적은 일반 투사체와 동일한 방식(ProjectileVfx.EnsureTrail)으로 AssassinDagger가 자동으로 TrailRenderer를 추가/설정하므로 프리팹에 따로 붙일 필요는 없고, AssassinDagger 인스펙터의 Trail Radius/Trail Seconds/Trail Width Multiplier로 두께/길이를 조절하면 됩니다. 단검이 부족하면 즉시 종료됩니다(이 패턴의 Cooldown Pattern Count는 0으로 설정하세요).";
        }

        if (actionType == typeof(AssassinSpawnCloneShootersAction))
        {
            return "Assassin 전용 액션입니다. 지정한 구역 안 랜덤 두 지점에 분신을 소환하고, 분신1/분신2/(옵션)보스 위치를 랜덤 순서로 섞어 발사 대기열에 채워둡니다. 실제 발사는 Fire Next Clone Shooter가 담당합니다.";
        }

        if (actionType == typeof(AssassinFireNextCloneShooterAction))
        {
            return "Assassin 전용 액션입니다. Spawn Clone Shooters가 채워둔 발사 대기열에서 다음 순서 하나를 꺼내 그 위치에서 투사체를 발사합니다. 분신 차례였다면 발사 직후 그 분신이 페이드아웃되며 사라집니다. 대기열이 비어 있으면 아무 것도 하지 않습니다. 이 노드를 여러 번(원하는 만큼) 배치하고 사이에 Wait 등을 끼워 넣어 템포를 자유롭게 조절하세요.";
        }

        if (actionType == typeof(AssassinSpawnRandomBombsAction))
        {
            return "Assassin 전용 액션입니다. 분신 스폰 구역(Clone Spawn Zone) 안에 폭탄을 여러 개 랜덤 배치합니다. 구역은 분신 소환과 공유하지만, 폭탄끼리 최소 간격(Min Separation Distance)과 플레이어와 최소거리(Min Distance From Player)는 이 액션에서 따로 지정합니다. Spawn Interval만큼 텀을 두고 하나씩 소환하며, Charge Seconds가 패링 유예 시간(=터질 때까지 남은 시간)입니다. 패링 판정이나 터질 때의 동작은 스폰되는 탄 프리팹 자신이 담당합니다.";
        }

        if (actionType == typeof(AssassinTeleportAroundPlayerAction))
        {
            return "Assassin 전용 액션입니다. 보스가 사라졌다가 플레이어로부터 Teleport Radius만큼 떨어진 원 위의 무작위 지점으로 순간이동해 다시 나타납니다. 목적지는 분신 스폰 구역(Clone Spawn Zone) 밖으로 나가지 않도록 제한됩니다. 사라짐/재등장 연출은 Spawn Clone Shooters가 보스 자신을 순간이동시킬 때와 동일한 방식(은신 알파 페이드)을 재사용합니다.";
        }

        if (actionType == typeof(HackerFireWireBranchAction))
        {
            return "바로 앞 Fire Wire Action의 결과를 분기합니다. Out1 연결은 플레이어 그랩 성공, Out2 연결은 그랩 실패 시에만 실행됩니다.";
        }

        if (actionType == typeof(FireProjectileBurstAction))
        {
            return "한 방향으로 여러 발을 연속 발사합니다. 머신건류 패턴의 발사 부분을 담당합니다.";
        }

        if (actionType == typeof(FireRandomConeBurstAction))
        {
            return "지정한 기준 방향을 중심으로 각도 범위 안에서 매 발 무작위 방향으로 난사합니다. Origin이 보스 자식이면 자식의 오른쪽 방향을 바라보는 방향으로 사용합니다.";
        }

        if (actionType == typeof(FirePlayerSideFanSweepAction))
        {
            return "보스에서 플레이어 옆 각도 방향으로 투사체를 하나씩 보내 한 막대기처럼 일렬로 멈춘 뒤, 대기 후 보스를 축으로 그 막대기를 플레이어 방향으로 동시에 휩쓸게 합니다.";
        }

        if (actionType == typeof(FireRadialEmissionAction))
        {
            return "원형 또는 부채꼴 투사체 방사를 Volley 목록 순서대로 실행합니다.";
        }

        if (actionType == typeof(FireSweepEmissionAction))
        {
            return "기준 방향을 좌우로 흔들며 연속 발사합니다. 준비 시간은 Windup과 분리해서 구성합니다.";
        }

        if (actionType == typeof(FireFanEmissionAction))
        {
            return "부채꼴 발리를 여러 번 발사합니다. 준비 시간은 Windup과 분리해서 구성합니다.";
        }

        if (actionType == typeof(FireDashFormationAction))
        {
            return "보스 대시 방향에 맞춰 투사체 벽 또는 대시 경로 탄을 정렬한 뒤 발사합니다. 대시 액션과 병렬로 배치해 타이밍을 맞추세요.";
        }

        if (actionType == typeof(FireDistanceTrailProjectilesAction))
        {
            return "이동 액션(Random Direction Move 등)과 P 포트로 병렬 연결해서 씁니다. 시간 간격이 아니라 실제 이동 거리(Move Distance)를 Bullet Count로 균등 분할한 간격마다 그 순간의 보스 위치에 탄을 스폰합니다. 이동 속도에 가속/감속 커브가 있어도 궤적상 간격이 일정합니다. Move Distance는 함께 실행되는 이동 액션의 Distance 값과 같게 맞춰야 합니다. 조준/충전 오버라이드를 강제하지 않으므로 탄 프리셋의 Aim At Player On Launch 등이 그대로 적용됩니다.";
        }

        if (actionType == typeof(FireParrySuppressionBaitAction))
        {
            return "패링으로 패턴을 억제(취소)시키는 시퀀스의 시작 액션입니다. ParryBaitRewardProjectile(플레이어에게 데미지를 주지 않는 순수 패링 타겟)을 소환합니다. Bait Duration 안에 패링되면: 지금 돌고 있는 패턴 전체가 취소되고(뒤에 배치한 액션들은 실행되지 않음), 보스가 Groggy Seconds 동안 그로기(무력화) 상태가 되며, 그 자리에 보상 탄이 원형(Reward Circle Radius, Reward Bullet Count)으로 뿌려집니다(보상 탄의 지속시간도 Groggy Seconds와 같게 맞춰집니다). Bait Duration 안에 패링되지 않으면 아무 효과 없이 사라지고 패턴이 그대로 이어집니다.";
        }

        if (actionType == typeof(BossAreaDamageAction))
        {
            return "투사체 없이, 보스 자신을 중심으로 원형 인디케이터를 띄우고 Windup Seconds가 지나면 그 범위 안 플레이어에게 광역 데미지를 줍니다. 인디케이터가 다 차면 Slam Bool Name을 켜고(Spawn Parryable Bomb과 동일한 방식), Impact Event Id로 지정한 Animation Event가 올 때까지 기다렸다가 실제 폭발/데미지를 발동합니다 — 내려찍는 애니메이션의 충돌 프레임과 정확히 맞출 수 있습니다. Fire Parry Suppression Bait와 P 포트로 병렬 연결하고 Windup Seconds를 그 액션의 Bait Duration Seconds와 같게 맞추면, 패링 가능 시간 내내 인디케이터가 보이다가 패링 성공 시 패턴 전체(이 액션 포함)가 취소되고 패링 실패 시 애니메이션 타이밍에 맞춰 폭발 데미지가 발동하는 '패링으로 억제 가능한 광역 공격'을 만들 수 있습니다.";
        }

        if (actionType == typeof(WanderAroundPlayerDistanceAction))
        {
            return "보스가 플레이어 주변의 최소-최대 거리 안에서 최근 덜 지나간 각도와 이전 목표점에서 떨어진 위치를 우선해 배회합니다.";
        }

        if (actionType == typeof(MoveBetweenMapPointsAction))
        {
            return "설정한 월드 좌표 시작점과 끝점 사이로 보스를 이동시킵니다.";
        }

        if (actionType == typeof(BossRandomDirectionMoveAction))
        {
            return "각도 범위 안에서 무작위로 방향을 골라 지정한 거리만큼 이동합니다. Strafe Around Player를 켜면 각도 범위 대신 플레이어를 바라보는 방향 기준 좌/우 중 무작위로 골라 이동합니다.";
        }

        if (actionType == typeof(SpawnChargedProjectileAction))
        {
            return "차징 투사체를 생성하고 핸들에 저장합니다. 이후 성장, 분열, 차징 종료 대기 액션이 같은 핸들을 사용합니다.";
        }

        if (actionType == typeof(ConfigureProjectileGrowthAction))
        {
            return "핸들에 저장된 차징 투사체의 크기 성장을 설정합니다.";
        }

        if (actionType == typeof(ConfigureRadialSplitAction))
        {
            return "핸들에 저장된 차징 투사체가 Launch 이후 방사형으로 분열되도록 설정합니다.";
        }

        if (actionType == typeof(WaitProjectileChargeEndAction))
        {
            return "핸들에 저장된 차징 투사체가 차징을 끝낼 때까지 대기합니다.";
        }

        if (actionType == typeof(AimBossChildAtPlayerAction))
        {
            return "보스 자식 오브젝트가 플레이어를 바라보도록 켜거나 끕니다. Start와 End 노드 한 쌍으로 사용합니다.";
        }

        if (actionType == typeof(CustomEventAction))
        {
            return "보스 오브젝트에 SendMessage 또는 BroadcastMessage 방식으로 커스텀 이벤트를 보냅니다.";
        }

        if (actionType == typeof(SpawnPrefabAction))
        {
            return "프리팹을 보스 기준 위치에 생성합니다. 필요하면 보스 자식으로 붙이고 일정 시간 뒤 제거합니다.";
        }

        if (actionType == typeof(PlaySfxAction))
        {
            return "지정한 SFX를 즉시 재생합니다.";
        }

        if (actionType == typeof(ConductorConductingCueAction))
        {
            return "Conductor 전용 전조 액션입니다. 단독 실행 시 전조만 그리고, 보스 본체 액션과 P로 병렬 연결되면 보스 액션의 마지막 구간을 덮어씁니다.";
        }

        if (actionType == typeof(ConductorParrySuppressionBaitAction))
        {
            return "Conductor 전용 액션입니다. 보스를 지정 위치로 이동시킨 뒤 드론 4기가 플레이어 주위를 축소 회전하며 접선 방향으로 계속 발사합니다. 중간에 무작위 드론을 따라다니는 보상 미끼를 패링하면 멈추고, 놓치면 최종 축소 시 플레이어가 한 번 피해를 입습니다.";
        }

        if (actionType == typeof(MinionSummonAction))
        {
            return "호스트 보스가 관리하는 미니언을 지정 수만큼 소환합니다.";
        }

        if (actionType == typeof(MinionEnsureCountAction))
        {
            return "패턴 시작에 필요한 미니언 수를 보장합니다.";
        }

        if (actionType == typeof(MinionAutoSummonIfNeededAction))
        {
            return "미니언이 부족할 때만 자동 보충 소환을 실행합니다.";
        }

        if (actionType == typeof(MinionSequentialFireAction))
        {
            return "현재 관리 중인 미니언들이 순서대로 하나씩 발사합니다.";
        }

        if (actionType == typeof(MinionRepeatFireAction))
        {
            return "미니언의 현재 이동을 유지한 채 Volley 목록 순서대로 반복 발사합니다.";
        }

        if (actionType == typeof(MinionOrbitAction))
        {
            return "미니언이 플레이어 주변을 궤도 이동합니다. 발사는 별도 Fire 액션과 병렬로 조합합니다.";
        }

        if (actionType == typeof(MinionWanderAction))
        {
            return "미니언이 현재 위치를 중심으로 기본 Wander 이동을 수행합니다. 발사는 별도 Fire 액션과 병렬로 조합합니다.";
        }

        if (actionType == typeof(MinionRadialBurstAction))
        {
            return "미니언 기준 방사형 발사를 Volley 목록 순서대로 실행합니다.";
        }

        if (actionType == typeof(MinionDashAction))
        {
            return "미니언이 지정 방향으로 즉시 돌진합니다. 발사는 별도 Fire 액션과 병렬로 조합합니다.";
        }

        if (actionType == typeof(MinionGatherAction))
        {
            return "미니언을 거리 기준 대표 미니언부터 플레이어 기준 배치로 집합시킵니다.";
        }

        if (actionType == typeof(MinionSideFireAction))
        {
            return "미니언의 현재 이동을 유지한 채 조준 방향 양옆으로 반복 발사합니다. BodySides 원점이면 몸체 양옆에서 나갑니다.";
        }

        if (actionType == typeof(MinionHoldPositionAction))
        {
            return "미니언을 현재 위치에 정지시킵니다. Repeat Fire와 병렬로 쓰면 정지 발사를 구성할 수 있습니다.";
        }

        if (actionType == typeof(MinionFormationAction))
        {
            return "미니언을 플레이어 기준 원형 진형으로 이동시킵니다. Side By Side를 켜면 보스 양옆의 원호에 배치합니다.";
        }

        if (actionType == typeof(MinionFormationStraightAction))
        {
            return "미니언을 플레이어 앞 또는 보스-플레이어 사이에 1자 진형으로 배치하고, 플레이어 이동에 맞춰 유지합니다.";
        }

        if (actionType == typeof(MinionPlayerPathAction))
        {
            return "액션 시작 시 플레이어 위치에 고정 정사각형을 만들고, 미니언을 시작점으로 보낸 뒤 수평, 수직, 좌우 대각선, 우좌 대각선 순서로 이동시킵니다.";
        }

        if (actionType == typeof(MinionConductorScoreLaneRushAction))
        {
            return "Conductor 전용 액션입니다. 1개 또는 2개 Side를 랜덤 선택하고, Volleys 풀의 Fire Timings 패턴을 중복 없이 골라 실행합니다.";
        }

        if (actionType == typeof(MinionConductorScoreLaneRushSpecialAction))
        {
            return "Conductor 전용 특수 액션입니다. 설정 좌표 기준으로 Top, Right, Bottom, Left 순서의 러시를 실행하고, Volleys 풀에서 Fire Timings 패턴을 중복 없이 랜덤 선택합니다.";
        }

        if (actionType == typeof(MinionConductorPlayerPathSideFireAction))
        {
            return "Conductor 전용 액션입니다. Player Path와 Side Fire를 순서대로 실행하고, 예상 탄 경로를 격자 인디케이터로 미리 표시합니다.";
        }

        if (actionType == typeof(MinionConductorFormationLineVolleyAction))
        {
            return "Conductor 전용 액션입니다. 미니언을 일렬 배치한 뒤 플레이어 방향 실선 인디케이터를 그리고, Volley 설정에 따라 해당 선 방향으로 투사체를 발사합니다.";
        }

        if (actionType == typeof(MinionConductorFanBladeAction))
        {
            return "Conductor 전용 액션입니다. 최대 4개의 드론을 중심점 기준 십자로 배치해 회전시키고, Volley별 탄막 이름/발사 구간/간격을 설정해 선풍기형 탄막을 만듭니다.";
        }

        if (actionType == typeof(ConductorSpawnTurretsAction))
        {
            return "Conductor 전용 액션입니다. Ground 위 목표 지점들을 고르고, 보스 위치에서 터렛을 날려 보낸 뒤 정착한 터렛이 자체 설정한 십자 탄을 발사합니다.";
        }

        if (actionType == typeof(MinionPatternCleanupAction))
        {
            return "미니언 명령, 동기화 발사, 대기 복귀를 패턴 종료용으로 정리합니다.";
        }

        return string.Empty;
    }
}

internal static class BossGraphActionFilterContext
{
    public static bool HasFilter { get; private set; }
    public static BossGraphNodeKind NodeKind { get; private set; }
    public static BossGraphActionCategoryAsset Categories { get; private set; }

    public static void Set(BossGraphNodeKind nodeKind, BossGraphActionCategoryAsset categories)
    {
        HasFilter = true;
        NodeKind = nodeKind;
        Categories = categories;
    }

    public static void Clear()
    {
        HasFilter = false;
        Categories = null;
    }

    public static bool IsAllowed(Type actionType)
    {
        if (!HasFilter)
        {
            return true;
        }

        BossGraphNodeKind defaultKind = BossGraphActionCategoryAsset.GetDefaultNodeKind(actionType);
        if (defaultKind == BossGraphNodeKind.Utility || defaultKind == BossGraphNodeKind.Minion)
        {
            return NodeKind == defaultKind;
        }

        BossGraphNodeKind actionNodeKind = Categories != null
            ? Categories.GetNodeKind(actionType)
            : defaultKind;
        if (actionNodeKind == BossGraphNodeKind.Utility || actionNodeKind == BossGraphNodeKind.Minion)
        {
            return defaultKind == actionNodeKind && NodeKind == actionNodeKind;
        }

        return actionNodeKind == NodeKind;
    }
}

internal static class BossGraphAimStartNodeOptions
{
    private static readonly List<string> startNodeIds = new();
    private static readonly Dictionary<string, string> startNodeLabels = new();
    private static bool hasContext;

    public static IReadOnlyList<string> StartNodeIds => startNodeIds;

    public static void Set(SerializedObject graphObject)
    {
        startNodeIds.Clear();
        startNodeLabels.Clear();
        hasContext = graphObject != null;
        if (graphObject == null)
        {
            return;
        }

        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeId = node.FindPropertyRelative("nodeId")?.stringValue;
            if (string.IsNullOrWhiteSpace(nodeId) || !IsAimStartNode(node))
            {
                continue;
            }

            if (!startNodeIds.Contains(nodeId))
            {
                startNodeIds.Add(nodeId);
                startNodeLabels[nodeId] = GetNodeDisplayName(stateNodes, node, i);
            }
        }
    }

    public static void Clear()
    {
        startNodeIds.Clear();
        startNodeLabels.Clear();
        hasContext = false;
    }

    public static string GetStartNodeLabel(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && startNodeLabels.TryGetValue(nodeId, out string label)
            ? label
            : nodeId;
    }

    public static bool ContainsStartNode(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId)
            || !hasContext
            || startNodeIds.Contains(nodeId);
    }

    private static bool IsAimStartNode(SerializedProperty node)
    {
        BossAction directAction = node.FindPropertyRelative("action")?.managedReferenceValue as BossAction;
        if (directAction is AimBossChildAtPlayerAction directAimAction)
        {
            return directAimAction.Mode == BossChildAimActionMode.Start;
        }

        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return false;
        }

        BossGraphActionAsset actionAsset = sequences.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
        BossAction action = actionAsset?.Action;
        return action is AimBossChildAtPlayerAction aimAction
            && aimAction.Mode == BossChildAimActionMode.Start;
    }

    private static string GetNodeDisplayName(SerializedProperty stateNodes, SerializedProperty node, int nodeIndex)
    {
        string baseName = GetNodeActionDisplayName(node);
        int totalCount = CountNodeDisplayNames(stateNodes, baseName);
        if (totalCount <= 1)
        {
            return baseName;
        }

        int occurrence = GetNodeDisplayNameOccurrence(stateNodes, baseName, nodeIndex);
        return $"{baseName} {Mathf.Max(1, occurrence):00}";
    }

    private static int CountNodeDisplayNames(SerializedProperty stateNodes, string baseName)
    {
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetNodeDisplayNameOccurrence(SerializedProperty stateNodes, string baseName, int nodeIndex)
    {
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 1;
        }

        int maxIndex = Mathf.Min(nodeIndex, stateNodes.arraySize - 1);
        int occurrence = 0;
        for (int i = 0; i <= maxIndex; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                occurrence++;
            }
        }

        return occurrence;
    }

    private static string GetNodeActionDisplayName(SerializedProperty node)
    {
        BossAction action = node.FindPropertyRelative("action")?.managedReferenceValue as BossAction;
        Type actionType = action?.GetType() ?? GetLegacyActionType(node);
        if (actionType == null)
        {
            return "Empty Action";
        }

        string label = BossGraphActionEditorUtility.GetActionLabel(actionType);
        int slashIndex = label.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < label.Length - 1)
        {
            label = label.Substring(slashIndex + 1);
        }

        const string actionSuffix = " Action";
        if (label.EndsWith(actionSuffix, StringComparison.Ordinal))
        {
            label = label.Substring(0, label.Length - actionSuffix.Length);
        }

        return string.IsNullOrWhiteSpace(label) ? "Empty Action" : label;
    }

    private static Type GetLegacyActionType(SerializedProperty node)
    {
        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return null;
        }

        BossGraphActionAsset actionAsset = sequences.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
        return actionAsset?.Action?.GetType();
    }
}

internal static class BossGraphProjectileNameOptions
{
    private static readonly List<string> names = new();

    public static IReadOnlyList<string> Names => names;

    public static void Set(IEnumerable<string> projectileNames)
    {
        names.Clear();
        if (projectileNames == null)
        {
            return;
        }

        foreach (string projectileName in projectileNames)
        {
            if (!string.IsNullOrWhiteSpace(projectileName) && !names.Contains(projectileName))
            {
                names.Add(projectileName);
            }
        }
    }

    public static void Clear()
    {
        names.Clear();
    }
}

internal static class BossGraphDrawerDescriptionGui
{
    public const float HelpBoxHeight = 38f;
    private static int suppressDescriptionsDepth;

    public static float Spacing => EditorGUIUtility.standardVerticalSpacing;
    public static bool SuppressDescriptions => suppressDescriptionsDepth > 0;
    public static float FoldoutDescriptionHeight => SuppressDescriptions ? Spacing : Spacing + HelpBoxHeight + Spacing;
    public static float InlineDescriptionHeight => SuppressDescriptions ? 0f : HelpBoxHeight + Spacing;

    public static IDisposable SuppressDescriptionsScope()
    {
        return new SuppressDescriptionScope();
    }

    public static float GetPropertyHeight(SerializedProperty property)
    {
        if (property == null)
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, true) + Spacing;
    }

    public static void DrawDescription(ref Rect lineRect, string text)
    {
        if (SuppressDescriptions)
        {
            return;
        }

        Rect descriptionRect = new(lineRect.x, lineRect.y, lineRect.width, HelpBoxHeight);
        EditorGUI.HelpBox(descriptionRect, text, MessageType.None);
        lineRect.y += HelpBoxHeight + Spacing;
    }

    public static void DrawProperty(ref Rect lineRect, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect propertyRect = new(lineRect.x, lineRect.y, lineRect.width, height);
        EditorGUI.PropertyField(propertyRect, property, true);
        lineRect.y += height + Spacing;
    }

    private sealed class SuppressDescriptionScope : IDisposable
    {
        private bool disposed;

        public SuppressDescriptionScope()
        {
            suppressDescriptionsDepth++;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            suppressDescriptionsDepth = Mathf.Max(0, suppressDescriptionsDepth - 1);
        }
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileOriginSpec))]
internal sealed class BossGraphProjectileOriginSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "bossChildPath",
        "bossChildPaths",
        "firstBossChildPath",
        "secondBossChildPath",
        "fallbackSpacing"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "투사체나 이펙트가 생성될 기준 위치를 정합니다. 보스 원점, 특정 자식, 자식 목록, 두 자식 교대를 사용할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(MinionGraphProjectileOriginSpec))]
internal sealed class MinionGraphProjectileOriginSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "minionChildPath",
        "minionChildPaths",
        "firstMinionChildPath",
        "secondMinionChildPath",
        "fallbackSpacing"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "미니언 투사체가 생성될 기준 위치를 정합니다. 미니언의 Projectile Origin, 루트, 특정 자식, 자식 목록, 두 자식 교대를 사용할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileAimSpec))]
internal sealed class BossGraphProjectileAimSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "angleDegrees"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "투사체가 향할 방향을 정합니다. 미니언 Fire에서는 가장 가까운 미니언의 플레이어 조준 방향을 공유할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphParticleEffectSettings))]
internal sealed class BossGraphParticleEffectSettingsDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "enabled",
        "color",
        "scale",
        "count"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, GetDescription(property.name));
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static string GetDescription(string propertyName)
    {
        return propertyName switch
        {
            "explosion" => "폭발 파티클 설정입니다. 켜면 발사/생성 지점에서 폭발 파티클을 재생합니다.",
            "smoke" => "연기 파티클 설정입니다. Smoke Interval은 이 연기가 반복 재생되는 간격입니다.",
            "muzzleFlash" => "총구 섬광 설정입니다. 켜면 발사 위치와 방향에 맞춰 섬광을 재생합니다.",
            _ => "파티클 이펙트의 사용 여부, 색, 크기, 개수를 정합니다."
        };
    }
}

[CustomPropertyDrawer(typeof(BossGraphCameraShakeSettings))]
internal sealed class BossGraphCameraShakeSettingsDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "enabled",
        "seconds",
        "distance",
        "frequency"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "카메라 흔들림 설정입니다. 켜면 지정 시간, 거리, 주기로 충격감을 줍니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphEffectSettings))]
internal sealed class BossGraphEffectSettingsDrawer : PropertyDrawer
{
    private const string ExplosionProperty = "explosion";
    private const string SmokeProperty = "smoke";
    private const string SmokeIntervalProperty = "smokeInterval";
    private const string MuzzleFlashProperty = "muzzleFlash";
    private const string CameraShakeProperty = "cameraShake";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing;
        height += BossGraphDrawerDescriptionGui.InlineDescriptionHeight;
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(ExplosionProperty));
        height += GetSmokePropertyHeight(property);
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(MuzzleFlashProperty));
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(CameraShakeProperty));
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "액션과 함께 재생할 공통 이펙트 묶음입니다. SFX는 각 액션 필드에 그대로 둡니다.");
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(ExplosionProperty));
        DrawSmokeProperty(ref lineRect, property);
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(MuzzleFlashProperty));
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(CameraShakeProperty));
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    private static float GetDefaultPropertyHeight(SerializedProperty property)
    {
        if (property == null)
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private static float GetSmokePropertyHeight(SerializedProperty property)
    {
        SerializedProperty smoke = property.FindPropertyRelative(SmokeProperty);
        if (smoke == null)
        {
            return 0f;
        }

        float height = EditorGUI.GetPropertyHeight(smoke, true) + EditorGUIUtility.standardVerticalSpacing;
        if (smoke.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private static void DrawDefaultProperty(ref Rect lineRect, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect propertyRect = new(lineRect.x, lineRect.y, lineRect.width, height);
        EditorGUI.PropertyField(propertyRect, property, true);
        lineRect.y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    private static void DrawSmokeProperty(ref Rect lineRect, SerializedProperty property)
    {
        SerializedProperty smoke = property.FindPropertyRelative(SmokeProperty);
        if (smoke == null)
        {
            return;
        }

        DrawDefaultProperty(ref lineRect, smoke);
        if (!smoke.isExpanded)
        {
            return;
        }

        SerializedProperty smokeInterval = property.FindPropertyRelative(SmokeIntervalProperty);
        if (smokeInterval == null)
        {
            return;
        }

        EditorGUI.indentLevel++;
        Rect intervalRect = new(lineRect.x, lineRect.y, lineRect.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(intervalRect, smokeInterval);
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.indentLevel--;
    }
}

[CustomPropertyDrawer(typeof(AimBossChildAtPlayerAction))]
internal sealed class AimBossChildAtPlayerActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        AddHeight(ref height, property.FindPropertyRelative("mode"));

        if (IsEndMode(property.FindPropertyRelative("mode")))
        {
            AddHeight(ref height, property.FindPropertyRelative("startNodeId"));
            AddHeight(ref height, property.FindPropertyRelative("targetPath"));
            AddHeight(ref height, property.FindPropertyRelative("activateOnStart"));
            AddHeight(ref height, property.FindPropertyRelative("flipYByFacing"));
            AddHeight(ref height, property.FindPropertyRelative("deactivateOnEnd"));
            AddHeight(ref height, property.FindPropertyRelative("deactivateOnPatternEnd"));
            return height;
        }

        AddHeight(ref height, property.FindPropertyRelative("targetPath"));
        AddHeight(ref height, property.FindPropertyRelative("activateOnStart"));
        AddHeight(ref height, property.FindPropertyRelative("flipYByFacing"));
        AddHeight(ref height, property.FindPropertyRelative("deactivateOnPatternEnd"));
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(lineRect, label, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty mode = property.FindPropertyRelative("mode");
        DrawProperty(ref lineRect, mode, new GUIContent("Mode"), false);

        bool isEndMode = IsEndMode(mode);
        if (isEndMode)
        {
            DrawProperty(ref lineRect, property.FindPropertyRelative("startNodeId"), new GUIContent("Start Node"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("targetPath"), new GUIContent("Target Path"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("activateOnStart"), new GUIContent("Activate On Start"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("flipYByFacing"), new GUIContent("Flip Y By Facing"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnEnd"), new GUIContent("Deactivate On End"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnPatternEnd"), new GUIContent("Deactivate On Pattern End"), true);
        }
        else
        {
            DrawProperty(ref lineRect, property.FindPropertyRelative("targetPath"), new GUIContent("Target Path"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("activateOnStart"), new GUIContent("Activate On Start"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("flipYByFacing"), new GUIContent("Flip Y By Facing"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnPatternEnd"), new GUIContent("Deactivate On Pattern End"), false);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static void AddHeight(ref float height, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        height += EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private static void DrawProperty(ref Rect lineRect, SerializedProperty property, GUIContent label, bool disabled)
    {
        if (property == null)
        {
            return;
        }

        lineRect.height = EditorGUI.GetPropertyHeight(property, true);
        using (new EditorGUI.DisabledScope(disabled))
        {
            EditorGUI.PropertyField(lineRect, property, label, true);
        }

        lineRect.y += lineRect.height + EditorGUIUtility.standardVerticalSpacing;
    }

    private static bool IsEndMode(SerializedProperty mode)
    {
        if (mode == null)
        {
            return false;
        }

        int endNameIndex = Array.IndexOf(mode.enumNames, nameof(BossChildAimActionMode.End));
        return mode.intValue == (int)BossChildAimActionMode.End || mode.enumValueIndex == endNameIndex;
    }
}

[CustomPropertyDrawer(typeof(MinionConductorScoreLaneRushSpecialAction))]
internal sealed class MinionConductorScoreLaneRushSpecialActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty iterator = property.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (ShouldSkipProperty(iterator))
            {
                continue;
            }

            height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = lineRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (ShouldSkipProperty(iterator))
                {
                    continue;
                }

                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, true);
                Rect propertyRect = new(position.x, y, position.width, propertyHeight);
                EditorGUI.PropertyField(propertyRect, iterator, true);
                y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static bool ShouldSkipProperty(SerializedProperty property)
    {
        return property != null
            && (property.name == "volleys"
                || property.name == "useTwoSides"
                || property.name == "standardDrawLaneIndicators"
                || property.name.StartsWith("standardLaneIndicator", StringComparison.Ordinal));
    }
}

[CustomPropertyDrawer(typeof(BossGraphNodeIdAttribute))]
internal sealed class BossGraphNodeIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        IReadOnlyList<string> startNodeIds = BossGraphAimStartNodeOptions.StartNodeIds;
        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new()
        {
            new(startNodeIds.Count == 0 ? "<Start 노드 없음>" : "<선택>")
        };

        for (int i = 0; i < startNodeIds.Count; i++)
        {
            values.Add(startNodeIds[i]);
            labels.Add(new GUIContent(BossGraphAimStartNodeOptions.GetStartNodeLabel(startNodeIds[i])));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (Start 노드 아님)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        if (nextIndex >= 0 && nextIndex < values.Count)
        {
            property.stringValue = values[nextIndex];
        }

        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileNameAttribute))]
internal sealed class BossGraphProjectileNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        IReadOnlyList<string> names = BossGraphProjectileNameOptions.Names;
        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<기본>") };
        for (int i = 0; i < names.Count; i++)
        {
            values.Add(names[i]);
            labels.Add(new GUIContent(names[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (목록 없음)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }
}

internal static class BossGraphSfxIdOptions
{
    private const double RefreshIntervalSeconds = 1d;

    private static readonly List<string> ids = new();
    private static double nextRefreshAt;

    public static IReadOnlyList<string> Ids
    {
        get
        {
            RefreshIfNeeded();
            return ids;
        }
    }

    private static void RefreshIfNeeded()
    {
        if (EditorApplication.timeSinceStartup < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
        ids.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SoundLibrary");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
            if (library == null)
            {
                continue;
            }

            foreach (string id in library.SfxIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);
    }
}

[CustomPropertyDrawer(typeof(BossGraphBossChildPathAttribute))]
internal sealed class BossGraphBossChildPathDrawer : PropertyDrawer
{
    private const float ClearButtonWidth = 22f;
    private const float DeleteButtonWidth = 24f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (property.propertyType == SerializedPropertyType.String)
        {
            return lineHeight;
        }

        if (IsStringList(property))
        {
            if (!property.isExpanded)
            {
                return lineHeight;
            }

            return lineHeight + spacing + property.arraySize * (lineHeight + spacing) + lineHeight;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawPathField(position, property, label, null);
            EditorGUI.EndProperty();
            return;
        }

        if (IsStringList(property))
        {
            DrawPathList(position, property, label);
            return;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    private static void DrawPathList(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (TryGetDroppedPath(lineRect, out string headerPath, true))
        {
            AddPath(property, headerPath);
            GUI.changed = true;
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < property.arraySize; i++)
            {
                lineRect.y += lineHeight + spacing;
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                DrawPathField(lineRect, element, new GUIContent($"Element {i}"), () => property.DeleteArrayElementAtIndex(i));
            }

            lineRect.y += lineHeight + spacing;
            Rect dropRect = EditorGUI.IndentedRect(lineRect);
            GUI.Box(dropRect, "Drop Hierarchy Item To Add", EditorStyles.helpBox);
            if (TryGetDroppedPath(dropRect, out string path, true))
            {
                AddPath(property, path);
                GUI.changed = true;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawPathField(Rect position, SerializedProperty property, GUIContent label, Action onDelete)
    {
        Rect fieldRect = position;
        float buttonWidth = onDelete != null ? DeleteButtonWidth : ClearButtonWidth;
        fieldRect.width -= buttonWidth + 2f;
        Rect buttonRect = position;
        buttonRect.xMin = fieldRect.xMax + 2f;

        Rect valueRect = EditorGUI.PrefixLabel(fieldRect, label);
        string value = property.stringValue;
        string displayValue = string.IsNullOrWhiteSpace(value) ? "<하이어러키에서 드롭>" : value;
        if (!string.IsNullOrWhiteSpace(value) && !BossGraphBossHierarchyOptions.ContainsPath(value))
        {
            displayValue = $"{value} (없음)";
        }

        GUI.Box(valueRect, displayValue, EditorStyles.textField);
        if (GUI.Button(buttonRect, onDelete != null ? "-" : "X"))
        {
            if (onDelete != null)
            {
                onDelete();
            }
            else
            {
                property.stringValue = string.Empty;
            }
        }

        if (TryGetDroppedPath(fieldRect, out string path, true))
        {
            property.stringValue = path;
            GUI.changed = true;
        }
    }

    private static bool TryGetDroppedPath(Rect dropRect, out string path, bool acceptOnPerform)
    {
        path = string.Empty;
        Event currentEvent = Event.current;
        if (!dropRect.Contains(currentEvent.mousePosition))
        {
            return false;
        }

        if (!TryResolveDroppedPath(out string droppedPath))
        {
            return false;
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            currentEvent.Use();
            return false;
        }

        if (currentEvent.type != EventType.DragPerform)
        {
            return false;
        }

        if (acceptOnPerform)
        {
            DragAndDrop.AcceptDrag();
        }

        path = droppedPath;
        currentEvent.Use();
        return true;
    }

    private static bool TryResolveDroppedPath(out string path)
    {
        path = string.Empty;
        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.BossChildPath);
        if (data is string droppedPath && !string.IsNullOrWhiteSpace(droppedPath))
        {
            path = droppedPath;
            return true;
        }

        UnityEngine.Object[] objectReferences = DragAndDrop.objectReferences;
        if (objectReferences == null)
        {
            return false;
        }

        for (int i = 0; i < objectReferences.Length; i++)
        {
            if (BossGraphBossHierarchyOptions.TryGetPath(objectReferences[i], out path))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPath(SerializedProperty property, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).stringValue == path)
            {
                return;
            }
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).stringValue = path;
        GUI.changed = true;
    }

    private static bool IsStringList(SerializedProperty property)
    {
        if (!property.isArray || property.propertyType == SerializedPropertyType.String)
        {
            return false;
        }

        return property.arraySize == 0 || property.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.String;
    }
}

[CustomPropertyDrawer(typeof(BossGraphMinionChildPathAttribute))]
internal sealed class BossGraphMinionChildPathDrawer : PropertyDrawer
{
    private const float ClearButtonWidth = 22f;
    private const float DeleteButtonWidth = 24f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (property.propertyType == SerializedPropertyType.String)
        {
            return lineHeight;
        }

        if (IsStringList(property))
        {
            if (!property.isExpanded)
            {
                return lineHeight;
            }

            return lineHeight + spacing + property.arraySize * (lineHeight + spacing) + lineHeight;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawPathField(position, property, label, null);
            EditorGUI.EndProperty();
            return;
        }

        if (IsStringList(property))
        {
            DrawPathList(position, property, label);
            return;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    private static void DrawPathList(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (TryGetDroppedPath(lineRect, out string headerPath, true))
        {
            AddPath(property, headerPath);
            GUI.changed = true;
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < property.arraySize; i++)
            {
                lineRect.y += lineHeight + spacing;
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                DrawPathField(lineRect, element, new GUIContent($"Element {i}"), () => property.DeleteArrayElementAtIndex(i));
            }

            lineRect.y += lineHeight + spacing;
            Rect dropRect = EditorGUI.IndentedRect(lineRect);
            GUI.Box(dropRect, "Drop Minion Hierarchy Item To Add", EditorStyles.helpBox);
            if (TryGetDroppedPath(dropRect, out string path, true))
            {
                AddPath(property, path);
                GUI.changed = true;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawPathField(Rect position, SerializedProperty property, GUIContent label, Action onDelete)
    {
        Rect fieldRect = position;
        float buttonWidth = onDelete != null ? DeleteButtonWidth : ClearButtonWidth;
        fieldRect.width -= buttonWidth + 2f;
        Rect buttonRect = position;
        buttonRect.xMin = fieldRect.xMax + 2f;

        Rect valueRect = EditorGUI.PrefixLabel(fieldRect, label);
        string value = property.stringValue;
        string displayValue = string.IsNullOrWhiteSpace(value) ? "<미니언 하이어러키에서 드롭>" : value;
        if (!string.IsNullOrWhiteSpace(value) && !BossGraphMinionHierarchyOptions.ContainsPath(value))
        {
            displayValue = $"{value} (없음)";
        }

        GUI.Box(valueRect, displayValue, EditorStyles.textField);
        if (GUI.Button(buttonRect, onDelete != null ? "-" : "X"))
        {
            if (onDelete != null)
            {
                onDelete();
            }
            else
            {
                property.stringValue = string.Empty;
            }
        }

        if (TryGetDroppedPath(fieldRect, out string path, true))
        {
            property.stringValue = path;
            GUI.changed = true;
        }
    }

    private static bool TryGetDroppedPath(Rect dropRect, out string path, bool acceptOnPerform)
    {
        path = string.Empty;
        Event currentEvent = Event.current;
        if (!dropRect.Contains(currentEvent.mousePosition))
        {
            return false;
        }

        if (!TryResolveDroppedPath(out string droppedPath))
        {
            return false;
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            currentEvent.Use();
            return false;
        }

        if (currentEvent.type != EventType.DragPerform)
        {
            return false;
        }

        if (acceptOnPerform)
        {
            DragAndDrop.AcceptDrag();
        }

        path = droppedPath;
        currentEvent.Use();
        return true;
    }

    private static bool TryResolveDroppedPath(out string path)
    {
        path = string.Empty;
        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.MinionChildPath);
        if (data is string droppedPath && !string.IsNullOrWhiteSpace(droppedPath))
        {
            path = droppedPath;
            return true;
        }

        UnityEngine.Object[] objectReferences = DragAndDrop.objectReferences;
        if (objectReferences == null)
        {
            return false;
        }

        for (int i = 0; i < objectReferences.Length; i++)
        {
            if (BossGraphMinionHierarchyOptions.TryGetPath(objectReferences[i], out path))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPath(SerializedProperty property, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).stringValue == path)
            {
                return;
            }
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).stringValue = path;
        GUI.changed = true;
    }

    private static bool IsStringList(SerializedProperty property)
    {
        if (!property.isArray || property.propertyType == SerializedPropertyType.String)
        {
            return false;
        }

        return property.arraySize == 0 || property.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.String;
    }
}

internal static class BossGraphBossHierarchyOptions
{
    private static Transform root;

    public static void Set(Transform bossRoot)
    {
        root = bossRoot;
    }

    public static void Clear()
    {
        root = null;
    }

    public static bool ContainsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        return root == null || root.Find(path) != null || FindChildRecursive(root, path) != null;
    }

    public static bool TryGetPath(UnityEngine.Object source, out string path)
    {
        path = string.Empty;
        Transform transform = source switch
        {
            GameObject gameObject => gameObject.transform,
            Transform sourceTransform => sourceTransform,
            _ => null
        };

        if (transform == null)
        {
            return false;
        }

        if (root == null)
        {
            path = transform.name;
            return !string.IsNullOrWhiteSpace(path);
        }

        if (transform == root)
        {
            return false;
        }

        if (!transform.IsChildOf(root))
        {
            return false;
        }

        path = GetRelativePath(root, transform);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string GetRelativePath(Transform parent, Transform child)
    {
        List<string> names = new();
        Transform current = child;
        while (current != null && current != parent)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

internal static class BossGraphMinionHierarchyOptions
{
    private static Transform root;

    public static void Set(Transform minionRoot)
    {
        root = minionRoot;
    }

    public static void Clear()
    {
        root = null;
    }

    public static bool ContainsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        return root == null || root.Find(path) != null || FindChildRecursive(root, path) != null;
    }

    public static bool TryGetPath(UnityEngine.Object source, out string path)
    {
        path = string.Empty;
        Transform transform = source switch
        {
            GameObject gameObject => gameObject.transform,
            Transform sourceTransform => sourceTransform,
            _ => null
        };

        if (transform == null)
        {
            return false;
        }

        if (root == null)
        {
            path = transform.name;
            return !string.IsNullOrWhiteSpace(path);
        }

        if (transform == root)
        {
            return false;
        }

        if (!transform.IsChildOf(root))
        {
            return false;
        }

        path = GetRelativePath(root, transform);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string GetRelativePath(Transform parent, Transform child)
    {
        List<string> names = new();
        Transform current = child;
        while (current != null && current != parent)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

[CustomPropertyDrawer(typeof(BossGraphSfxIdAttribute))]
internal sealed class BossGraphSfxIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        IReadOnlyList<string> ids = BossGraphSfxIdOptions.Ids;
        if (property.propertyType != SerializedPropertyType.String || ids.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<없음>") };
        for (int i = 0; i < ids.Count; i++)
        {
            values.Add(ids[i]);
            labels.Add(new GUIContent(ids[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (목록 없음)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }
}

internal static class BossGraphBgmIdOptions
{
    private const double RefreshIntervalSeconds = 1d;

    private static readonly List<string> ids = new();
    private static double nextRefreshAt;

    public static IReadOnlyList<string> Ids
    {
        get
        {
            RefreshIfNeeded();
            return ids;
        }
    }

    private static void RefreshIfNeeded()
    {
        if (EditorApplication.timeSinceStartup < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
        ids.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SoundLibrary");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
            if (library == null)
            {
                continue;
            }

            foreach (string id in library.BgmIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);
    }
}

[CustomPropertyDrawer(typeof(BossGraphBgmIdAttribute))]
internal sealed class BossGraphBgmIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        IReadOnlyList<string> ids = BossGraphBgmIdOptions.Ids;
        if (property.propertyType != SerializedPropertyType.String || ids.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<없음>") };
        for (int i = 0; i < ids.Count; i++)
        {
            values.Add(ids[i]);
            labels.Add(new GUIContent(ids[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (목록 없음)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }
}
