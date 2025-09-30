using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.DistanceTeleport
{
    public static class DistanceTeleportLogic
    {
        public const float PET_PLACEMENT_BUFFER = 0.15f;

        public static string GetPrefabName(Character c)
        {
            return c != null ? c.name.Replace("(Clone)", "") : "UnknownCharacter";
        }

        public static float GetPetHeight(Character character)
        {
            if (character == null)
            {
                return 1f;
            }

            // Alte Schreibweise, kompatibel mit C# 7.3
            CapsuleCollider capsule = character.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                return capsule.height * character.transform.localScale.y;
            }

            // Alte Schreibweise, kompatibel mit C# 7.3
            Collider genericCollider = character.GetComponent<Collider>();
            if (genericCollider != null)
            {
                return genericCollider.bounds.size.y;
            }

            return 1f;
        }
                public static void TeleportPetToActualPosition(Character petCharacter, Vector3 targetPosition, Player teleportingPlayer)
        {
            if (petCharacter == null) return;

            ZNetView nview = petCharacter.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            string prefabName = GetPrefabName(petCharacter);
            BetterTamesPlugin.LogIfDebug($"Teleporting {prefabName} to {targetPosition}", DebugFeature.TeleportFollow);

            Quaternion targetRotation = Quaternion.LookRotation((teleportingPlayer.transform.position - targetPosition).normalized);
            petCharacter.transform.position = targetPosition;
            petCharacter.transform.rotation = targetRotation;

            // --- KORREKTUR 1 ---
            // Alte, kompatible Schreibweise
            Rigidbody rb = petCharacter.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // --- KORREKTUR 2 ---
            // Alte, kompatible Schreibweise
            MonsterAI ai = petCharacter.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.StopMoving();
                ai.SetFollowTarget(teleportingPlayer.gameObject);
            }

            ZDO zdo = nview.GetZDO();
            zdo.SetPosition(targetPosition);
            zdo.SetRotation(targetRotation);

            // Sende RPC an alle Clients, um die Position zu synchronisieren
            ZPackage pkg = new ZPackage();
            pkg.Write(targetPosition);
            pkg.Write(targetRotation);
            string zdoID_str = $"{zdo.m_uid.UserID}:{zdo.m_uid.ID}";

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_TELEPORT_SYNC, zdoID_str, pkg);
        }

        /// <summary>
        /// Berechnet Spawn-Positionen für Haustiere in einem Bogen hinter dem Spieler.
        /// </summary>
        public static List<Vector3> CalculateDistributedSpawnPositions(Vector3 playerPos, Quaternion playerRot, int petCount)
        {
            var positions = new List<Vector3>();
            if (petCount == 0) return positions;

            float baseDistance = 3f; // Wie weit hinter dem Spieler
            float angularSpread = 120f; // Gesamtbreite des Bogens in Grad
            float verticalOffset = 0.2f; // Leichte Anhebung vom Boden

            for (int i = 0; i < petCount; i++)
            {
                // Berechnet den Winkel für jedes Tier, zentriert hinter dem Spieler
                float angle = (petCount > 1)
                    ? -angularSpread / 2f + (i * (angularSpread / (petCount - 1)))
                    : 0f;

                // Berechnet die Position auf dem Bogen
                Vector3 direction = Quaternion.Euler(0, angle, 0) * (playerRot * Vector3.back);
                Vector3 spawnPos = playerPos + direction * baseDistance;

                // Finde den Boden und korrigiere die Y-Position
                if (ZoneSystem.instance.FindFloor(spawnPos + Vector3.up * 2f, out float floorHeight))
                {
                    spawnPos.y = floorHeight + verticalOffset;
                }

                positions.Add(spawnPos);
            }
            return positions;
        }
    }
}