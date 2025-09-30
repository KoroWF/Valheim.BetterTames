using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.DistanceTeleport
{
    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    public static class DistanceTeleportPatch
    {
        private const float TELEPORT_CHECK_INTERVAL = 2f; // Prüfen wir etwas häufiger
        private static readonly Dictionary<ZDOID, float> nextTeleportCheckTime = new Dictionary<ZDOID, float>();

        [HarmonyPostfix]
        public static void Postfix(MonsterAI __instance)
        {
            // --- Frühe Ausstiege (Guard Clauses) für bessere Performance ---
            if (!BetterTamesPlugin.ConfigInstance.Tames.TeleportFollowEnabled.Value) return;
            if (ZNet.instance == null) return;

            Character character = __instance.GetComponent<Character>();
            if (character == null || !character.IsTamed()) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return; // Nur der Owner soll die Logik ausführen

            GameObject followTarget = __instance.GetFollowTarget();
            if (followTarget == null) return;

            // --- Timer-Logik, um nicht in jedem Frame die Distanz zu prüfen ---
            ZDOID petZDOID = nview.GetZDO().m_uid;
            if (Time.time < (nextTeleportCheckTime.TryGetValue(petZDOID, out float checkTime) ? checkTime : 0f))
            {
                return;
            }
            nextTeleportCheckTime[petZDOID] = Time.time + TELEPORT_CHECK_INTERVAL;

            // --- Kernlogik: Distanz prüfen und Teleport delegieren ---
            float teleportThreshold = BetterTamesPlugin.ConfigInstance.Tames.TeleportOnDistanceMaxRange.Value;
            float distanceSqr = (character.transform.position - followTarget.transform.position).sqrMagnitude;

            if (distanceSqr > teleportThreshold * teleportThreshold)
            {
                // Prüfen, ob der Spieler in einem Dungeon ist (Y > 2000 ist ein gängiger Indikator)
                if (followTarget.transform.position.y > 2000f)
                {
                    BetterTamesPlugin.LogIfDebug($"Player is likely in a dungeon (Y > 2000). Preventing teleport for {character.m_name}.", DebugFeature.TeleportFollow);
                    return;
                }

                BetterTamesPlugin.LogIfDebug($"{character.m_name} is too far away. Attempting teleport.", DebugFeature.TeleportFollow);

                // **DELEGIERUNG AN DIE LOGIK-KLASSE**
                // 1. Berechne eine geeignete Position hinter dem Spieler.
                Player player = followTarget.GetComponent<Player>();
                var spawnPositions = DistanceTeleportLogic.CalculateDistributedSpawnPositions(player.transform.position, player.transform.rotation, 1);

                if (spawnPositions.Count > 0)
                {
                    // 2. Führe den Teleport mit der berechneten Position durch.
                    DistanceTeleportLogic.TeleportPetToActualPosition(character, spawnPositions[0], player);
                }
            }
        }
    }
}