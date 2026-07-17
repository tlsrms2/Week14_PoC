using TMPro;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class HackerPlayerHackStatus : MonoBehaviour
    {
        private PlayerCombatController player;
        private TextMeshPro debugText;
        private int currentHack;
        private int maximumHack = 1;
        private float parryDisabledEndsAt;
        private bool isParrySuppressed;

        internal static void Apply(PlayerCombatController target, int amount, int maximum, float disableSeconds)
        {
            if (target == null)
            {
                return;
            }

            HackerPlayerHackStatus status = target.GetComponent<HackerPlayerHackStatus>();
            if (status == null)
            {
                status = target.gameObject.AddComponent<HackerPlayerHackStatus>();
            }

            status.AddHack(amount, maximum, disableSeconds);
        }

        internal static void Clear(PlayerCombatController target)
        {
            target?.GetComponent<HackerPlayerHackStatus>()?.ClearHack();
        }

        private void Awake()
        {
            player = GetComponent<PlayerCombatController>();
            CreateDebugText();
            RefreshDisplay();
        }

        private void Update()
        {
            if (isParrySuppressed && Time.time >= parryDisabledEndsAt)
            {
                isParrySuppressed = false;
                currentHack = 0;
                PlayerCombatController.PopParrySuppression();
                player?.SetHackerParryVisual(false);
                RefreshDisplay();
            }

            if (isParrySuppressed)
            {
                RefreshDisplay();
            }
        }

        private void OnDestroy()
        {
            if (isParrySuppressed)
            {
                PlayerCombatController.PopParrySuppression();
            }

            if (player != null)
            {
                player.SetHackerParryVisual(false);
            }
        }

        private void AddHack(int amount, int maximum, float disableSeconds)
        {
            maximumHack = Mathf.Max(1, maximum);
            if (isParrySuppressed)
            {
                parryDisabledEndsAt = Mathf.Max(parryDisabledEndsAt, Time.time + Mathf.Max(0.1f, disableSeconds));
                return;
            }

            currentHack = Mathf.Clamp(currentHack + Mathf.Max(1, amount), 0, maximumHack);
            if (currentHack >= maximumHack)
            {
                isParrySuppressed = true;
                parryDisabledEndsAt = Time.time + Mathf.Max(0.1f, disableSeconds);
                PlayerCombatController.PushParrySuppression();
                player?.SetHackerParryVisual(true);
            }

            RefreshDisplay();
        }

        private void ClearHack()
        {
            currentHack = 0;
            if (isParrySuppressed)
            {
                isParrySuppressed = false;
                PlayerCombatController.PopParrySuppression();
                player?.SetHackerParryVisual(false);
            }

            RefreshDisplay();
        }

        private void CreateDebugText()
        {
            GameObject textObject = new("HackerHackDebugText");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            debugText = textObject.AddComponent<TextMeshPro>();
            debugText.alignment = TextAlignmentOptions.Center;
            debugText.fontSize = 3.5f;
            debugText.sortingOrder = 30;
            debugText.color = new Color(0.3f, 0.9f, 1f, 1f);
        }

        private void RefreshDisplay()
        {
            if (debugText == null)
            {
                return;
            }

            if (isParrySuppressed)
            {
                debugText.color = new Color(1f, 0.2f, 0.2f, 1f);
                debugText.text = $"HACKED {Mathf.Max(0f, parryDisabledEndsAt - Time.time):0.0}s";
                return;
            }

            debugText.color = new Color(0.3f, 0.9f, 1f, 1f);
            debugText.text = $"HACK {currentHack}/{maximumHack}";
        }
    }
}
