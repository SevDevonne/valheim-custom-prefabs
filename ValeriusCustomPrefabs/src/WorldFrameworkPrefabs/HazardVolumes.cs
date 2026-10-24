using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ValeriusCustomPrefabs
{
    public sealed class HazardVolume : MonoBehaviour
    {
        internal const float TickSeconds = 1.5f;
        internal const bool AllowDamageToPlayers = true;
        internal const float FireDamage = 15f;
        internal const float PoisonDamage = 12f;
        internal const float FrostDamage = 12f;
        internal const float LightningDamage = 20f;
        internal const float PhysicalDamage = 16f;

        [SerializeField] private string kind = "lava";
        [SerializeField] private Vector3 size = new Vector3(4f, 0.65f, 4f);

        private ZNetView nview;
        private float nextCheck;
        private readonly HashSet<int> deduplicated = new HashSet<int>();
        private static readonly Collider[] hits = new Collider[256];

        private static readonly Dictionary<string, float> sharedLastHit =
            new Dictionary<string, float>();
        private static float lastCleanup;

        private void Awake()
        {
            nview = GetComponent<ZNetView>();
        }

        private void FixedUpdate()
        {
            var plugin = ValeriusCustomPrefabs.Instance;
            if (plugin == null) return;

            if (nview == null)
                nview = GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || nview.GetZDO() == null)
                return;

            float interval = Mathf.Max(0.25f, TickSeconds);
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + interval;
            deduplicated.Clear();

            if (Time.time - lastCleanup > 60f)
            {
                lastCleanup = Time.time;
                var expired = new List<string>();
                foreach (var entry in sharedLastHit)
                    if (Time.time - entry.Value > 120f) expired.Add(entry.Key);
                foreach (var key in expired) sharedLastHit.Remove(key);
            }

            int count = Physics.OverlapBoxNonAlloc(
                transform.position, size / 2f, hits, transform.rotation);

            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i];
                if (collider == null) continue;

                Character target = collider.GetComponentInParent<Character>();
                if (target == null || !deduplicated.Add(target.GetInstanceID())) continue;
                if (target is Player && !AllowDamageToPlayers) continue;

                ZNetView targetView = target.GetComponent<ZNetView>();
                if (targetView == null || !targetView.IsValid() || !targetView.IsOwner())
                    continue;

                string key = target.GetInstanceID() + ":" + kind;
                float last;
                if (sharedLastHit.TryGetValue(key, out last) &&
                    Time.time - last < interval)
                    continue;

                var hit = new HitData();
                float amount;
                switch (kind)
                {
                    case "poison":
                        amount = PoisonDamage;
                        hit.m_damage.m_poison = amount;
                        break;
                    case "frost":
                        amount = FrostDamage;
                        hit.m_damage.m_frost = amount;
                        break;
                    case "lightning":
                        amount = LightningDamage;
                        hit.m_damage.m_lightning = amount;
                        break;
                    case "physical":
                        amount = PhysicalDamage;
                        hit.m_damage.m_pierce = amount;
                        break;
                    default:
                        amount = FireDamage;
                        hit.m_damage.m_fire = amount;
                        break;
                }

                if (amount <= 0f) continue;
                sharedLastHit[key] = Time.time;
                target.Damage(hit);
            }
        }
    }
}
