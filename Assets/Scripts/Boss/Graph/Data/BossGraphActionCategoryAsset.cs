using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public enum BossGraphNodeKind
    {
        Attack,
        Move,
        Animation,
        Utility
    }

    [CreateAssetMenu(menuName = "Week14/Boss/Boss Graph Action Categories", fileName = "BossGraphActionCategories")]
    public sealed class BossGraphActionCategoryAsset : ScriptableObject
    {
        [SerializeField] private List<BossGraphActionCategoryRule> rules = new();

        public IReadOnlyList<BossGraphActionCategoryRule> Rules => rules;

        public BossGraphNodeKind GetNodeKind(Type actionType)
        {
            if (TryGetConfiguredNodeKind(actionType, out BossGraphNodeKind nodeKind))
            {
                return nodeKind;
            }

            return GetDefaultNodeKind(actionType);
        }

        public bool IsActionAllowed(Type actionType, BossGraphNodeKind nodeKind)
        {
            return GetNodeKind(actionType) == nodeKind;
        }

        public bool HasActionKind(Type actionType)
        {
            return TryGetConfiguredNodeKind(actionType, out _);
        }

        public void SetActionKind(Type actionType, BossGraphNodeKind nodeKind)
        {
            if (actionType == null || !typeof(BossAction).IsAssignableFrom(actionType))
            {
                return;
            }

            string typeName = actionType.AssemblyQualifiedName;
            string fullName = actionType.FullName;
            for (int i = 0; i < rules.Count; i++)
            {
                BossGraphActionCategoryRule rule = rules[i];
                if (rule != null && (rule.ActionTypeName == typeName || rule.ActionTypeName == fullName))
                {
                    rule.SetNodeKind(nodeKind);
                    return;
                }
            }

            rules.Add(new BossGraphActionCategoryRule(typeName, nodeKind));
        }

        public static BossGraphNodeKind GetDefaultNodeKind(Type actionType)
        {
            if (actionType == typeof(MoveTowardPlayerAction)
                || actionType == typeof(MoveBodyRootLocalAction)
                || actionType == typeof(ResetBodyRootLocalAction))
            {
                return BossGraphNodeKind.Move;
            }

            if (actionType == typeof(PlayAnimationAction)
                || actionType == typeof(WaitForAnimationEventAction))
            {
                return BossGraphNodeKind.Animation;
            }

            if (actionType == typeof(WaitAction)
                || actionType == typeof(AimBossChildAtPlayerAction))
            {
                return BossGraphNodeKind.Utility;
            }

            return BossGraphNodeKind.Attack;
        }

        private bool TryGetConfiguredNodeKind(Type actionType, out BossGraphNodeKind nodeKind)
        {
            nodeKind = BossGraphNodeKind.Attack;
            if (actionType == null)
            {
                return false;
            }

            string assemblyQualifiedName = actionType.AssemblyQualifiedName;
            string fullName = actionType.FullName;
            for (int i = 0; i < rules.Count; i++)
            {
                BossGraphActionCategoryRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.ActionTypeName == assemblyQualifiedName || rule.ActionTypeName == fullName)
                {
                    nodeKind = rule.NodeKind;
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class BossGraphActionCategoryRule
    {
        [SerializeField] private string actionTypeName;
        [SerializeField] private BossGraphNodeKind nodeKind;

        public BossGraphActionCategoryRule()
        {
        }

        public BossGraphActionCategoryRule(string actionTypeName, BossGraphNodeKind nodeKind)
        {
            this.actionTypeName = actionTypeName;
            this.nodeKind = nodeKind;
        }

        public string ActionTypeName => actionTypeName;
        public BossGraphNodeKind NodeKind => nodeKind;

        public void SetNodeKind(BossGraphNodeKind nextNodeKind)
        {
            nodeKind = nextNodeKind;
        }
    }
}
