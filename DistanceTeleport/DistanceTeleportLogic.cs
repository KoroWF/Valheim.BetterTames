using BepInEx.Configuration;
using BetterTames.DistanceTeleport;
using System;
using System.Collections.Generic;
using UnityEngine;
using BetterTames.ConfigSynchronization;
namespace BetterTames
{

    public static class DistanceTeleportLogic
    {
        private static readonly int groundLayerMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker", "vehicle");

        public const float TELEPORT_CHECK_INTERVAL = 5f;

        public static void ExecuteTeleportBehindPlayer(Character characterToTeleport, GameObject followTarget)
        {
            // --- Beginn des extrahierten Codes ---

            ZNetView nview = characterToTeleport.GetComponent<ZNetView>();
            ZDO zdo = nview.GetZDO();

            Vector3 playerPosition = followTarget.transform.position;
            Quaternion playerRotation = followTarget.transform.rotation;

            // Log-Nachricht, die den Start des Teleports anzeigt
            BetterTamesPlugin.LogIfDebug($"Attempting teleport for {characterToTeleport.m_name}.", DebugFeature.TeleportFollow);

            // Dungeon-Check, um Teleport in Dungeons zu vermeiden
            if (playerPosition.y > 1000f)
            {
                BetterTamesPlugin.LogIfDebug($"Player Y position {playerPosition.y:F1} is > 1000. Preventing pet teleport.", DebugFeature.TeleportFollow);
                return;
            }

            // Berechnung der Vektoren basierend auf der Spielerrotation
            Vector3 forwardVec = playerRotation * Vector3.forward;
            Vector3 rightVec = playerRotation * Vector3.right;

            // Berechnung des Tier-Radius für korrekten Abstand
            float petRadius = 1f;
            CapsuleCollider capsule = characterToTeleport.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                petRadius = capsule.radius * Mathf.Max(characterToTeleport.transform.localScale.x, characterToTeleport.transform.localScale.z);
            }
            else
            {
                Collider collider = characterToTeleport.GetComponent<Collider>();
                if (collider != null)
                {
                    petRadius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
                }
            }

            // Berechnung der zufälligen Zielposition hinter dem Spieler
            float minDistance = Mathf.Max(8f, petRadius + 0.5f);
            float maxDistance = minDistance + 3f;
            float sideOffsetRange = Mathf.Max(10f, petRadius * 1.5f);

            float distanceBehind = UnityEngine.Random.Range(minDistance, maxDistance);
            float sideOffset = UnityEngine.Random.Range(-sideOffsetRange / 2f, sideOffsetRange / 2f);

            // Wenn das Tier sich in der Stun-Phase von PetProtection befindet,
            // dann 30f weiter hinter dem Spieler platzieren.
            try
            {
                if (zdo != null && zdo.GetBool("isRecoveringFromStun", false))
                {
                    BetterTamesPlugin.LogIfDebug($"Pet {characterToTeleport.m_name} is in pet-protection stun phase — increasing teleport distance by 30f.", DebugFeature.TeleportFollow);
                    distanceBehind += 30f;
                }
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception while checking stun flag for {characterToTeleport.m_name}: {ex}", DebugFeature.TeleportFollow);
            }

            Vector3 positionBehind = -forwardVec * distanceBehind;
            Vector3 positionWithSideOffset = rightVec * sideOffset;
            Vector3 targetPosition = playerPosition + positionBehind + positionWithSideOffset;

            // Bodensuche per Raycast, um die korrekte Y-Position zu finden
            if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out RaycastHit hitInfo, 10f, DistanceTeleportLogic.groundLayerMask))
            {
                targetPosition.y = hitInfo.point.y + 1f;
            }
            else
            {
                targetPosition.y = playerPosition.y; // Fallback, falls kein Boden gefunden wird
                BetterTamesPlugin.LogIfDebug("No ground found via Raycast for auto-teleport of " + characterToTeleport.m_name + ", using target Y position.", DebugFeature.TeleportFollow);
            }

            // Finale Rotation und Teleport-Aktion
            Quaternion targetRotation;
            // Guard against zero-length forward vector that would cause LookRotation to throw.
            if (forwardVec.sqrMagnitude < 1e-6f)
            {
                // Fallback to player's rotation if forward vector is invalid
                targetRotation = playerRotation;
            }
            else
            {
                // Use normalized forward vector and ensure quaternion is normalized
                targetRotation = Quaternion.LookRotation(forwardVec.normalized);
            }
            targetRotation = targetRotation.normalized;

            // Manuelles Setzen von Position und Rotation
            characterToTeleport.transform.position = targetPosition;
            characterToTeleport.transform.rotation = targetRotation;

            // Manuelles Aktualisieren der Netzwerk-Daten (ZDO)
            zdo.SetPosition(targetPosition);
            zdo.SetRotation(targetRotation);

            // Aufwecken der Physik-Komponente
            Rigidbody rigidbody = characterToTeleport.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.WakeUp();
            }

            // Senden der Synchronisations-Nachricht an alle Spieler
            BetterTamesPlugin.LogIfDebug($"Teleported {characterToTeleport.m_name} to {targetPosition} (behind followTarget). Sending RPC.", DebugFeature.TeleportFollow);
            ZPackage package = new ZPackage();
            package.Write(targetPosition);
            // write a normalized quaternion to reduce chance of non-unit quaternions being serialized
            package.Write(targetRotation.normalized);
            string zdoIDString = $"{zdo.m_uid.UserID}:{zdo.m_uid.ID}";

            // Finde den aktuellen Owner (Client) des ZDO
            long ownerID = zdo.GetOwner();
            ZNetPeer senderID = ZNet.instance.GetPeer(ownerID);

            ZRoutedRpc.instance.InvokeRoutedRPC(senderID.m_uid, "BT_TeleportSync", new object[] { zdoIDString, package });

            //ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BT_TeleportSync", new object[] { zdoIDString, package });

            // --- Ende des extrahierten Codes ---
        }

        /// <summary>
        /// Überprüft die Distanz und führt bei Bedarf einen Teleport aus.
        /// Dies ist die vollständige Logik aus dem alten DistanceTeleportPatch.Postfix, umgebaut als wiederverwendbare Methode.
        /// </summary>
        /// <param name="tame">Das gezähmte Tier (Character).</param>
        /// <returns>True, wenn ein Teleport ausgeführt wurde.</returns>
        public static bool CheckDistanceAndTeleport(Character tame)
        {
            // Früher Abbruch, wenn Feature deaktiviert
            if (BetterTamesPlugin.ConfigInstance?.Tames.TeleportFollowEnabled?.Value != true)
            {
                return false;
            }

            if (ZNet.instance == null)
            {
                return false;
            }

            ZNetView component = tame.GetComponent<ZNetView>();
            if (component == null || !component.IsValid())
            {
                return false;
            }
            ZDO zdo = component.GetZDO();
            if (zdo == null)
            {
                return false;
            }

            if (!tame.IsTamed())
            {
                return false;
            }

            GameObject followTarget = tame.GetComponent<MonsterAI>().GetFollowTarget();
            if (followTarget == null)
            {
                return false;
            }

            Vector3 position = tame.transform.position;
            Vector3 position2 = followTarget.transform.position;

            MonsterAI monsterAI = tame.GetComponent<MonsterAI>();
            Character targetCreature = monsterAI.GetTargetCreature();
            StaticTarget staticTarget = monsterAI.GetStaticTarget();
            bool flag4 = (targetCreature != null && BaseAI.IsEnemy(tame, targetCreature)) || staticTarget != null;
            if (Vector3.Distance(position, position2) < 5f && !flag4)
            {
                monsterAI.StopMoving();
            }

            if (!component.IsOwner())
            {
                return false;
            }

            // FIX: Cast zu float (Mathf.Max gibt int zurück, wenn Value int ist)
            float num4 = Mathf.Max((float)BetterTamesPlugin.ConfigInstance.Tames.TeleportOnDistanceMaxRange.Value, 10f);
            float sqrMagnitude = (position - position2).sqrMagnitude;
            float num5 = num4 * num4;
            if (sqrMagnitude <= num5)
            {
                BetterTamesPlugin.LogIfDebug($"Distance check for {tame.m_name}: {Mathf.Sqrt(sqrMagnitude):F1}m < {num4}m threshold. No teleport.", DebugFeature.TeleportFollow);
                return false;
            }

            BetterTamesPlugin.LogIfDebug(string.Format("DistanceSqr {0:F1} > {1:F1}. Attempting teleport for {2}.", sqrMagnitude, num5, tame.m_name), DebugFeature.TeleportFollow);

            // Rufe die Teleport-Methode auf
            ExecuteTeleportBehindPlayer(tame, followTarget);
            return true;
        }

        public static List<Vector3> CalculateDistributedSpawnPositions(Vector3 playerPos, Quaternion playerRot, int petCount)
        {
            var positions = new List<Vector3>();
            if (petCount == 0) return positions;

            float baseDistance = 3f;
            float angularSpread = 120f;
            float verticalOffset = 0.2f;

            for (int i = 0; i < petCount; i++)
            {
                float angle = (petCount > 1) ? -angularSpread / 2f + (i * (angularSpread / (petCount - 1))) : 0f;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * (playerRot * Vector3.back);
                Vector3 spawnPos = playerPos + direction * baseDistance;

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