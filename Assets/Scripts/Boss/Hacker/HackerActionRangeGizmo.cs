using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal interface IHackerApproachRangeProvider
    {
        float ApproachStartDistance { get; }
        float ApproachStopDistance { get; }
    }

    internal static class HackerActionRangeGizmo
    {
        private static readonly Color StartDistanceColor = new(0.2f, 0.75f, 1f, 0.9f);
        private static readonly Color StopDistanceColor = new(1f, 0.55f, 0.1f, 0.9f);

        internal static void Draw(Vector3 origin, BossGraphAsset graph)
        {
            if (graph?.StateNodes == null)
            {
                return;
            }

            HashSet<BossAction> drawnActions = new();
            IReadOnlyList<BossStateNode> nodes = graph.StateNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                DrawAction(origin, nodes[i]?.Action, drawnActions);
            }
        }

        private static void DrawAction(Vector3 origin, BossAction action, ISet<BossAction> drawnActions)
        {
            if (action is not IHackerApproachRangeProvider approachRange || !drawnActions.Add(action))
            {
                return;
            }

            float startDistance = Mathf.Max(0f, approachRange.ApproachStartDistance);
            float stopDistance = Mathf.Min(startDistance, Mathf.Max(0f, approachRange.ApproachStopDistance));

            Gizmos.color = StartDistanceColor;
            Gizmos.DrawWireSphere(origin, startDistance);
            Gizmos.color = StopDistanceColor;
            Gizmos.DrawWireSphere(origin, stopDistance);
        }
    }
}
