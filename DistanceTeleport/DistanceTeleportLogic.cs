using BetterTames.DistanceTeleport;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames
{
    // Token: 0x0200000A RID: 10
    public static class DistanceTeleportLogic
    {
        public static readonly int groundLayerMask = LayerMask.GetMask(
            "Default", "static_solid", "Default_small", "piece", "terrain", "blocker", "vehicle");

        /// <summary>
        /// Dies ist die exakte Teleport-Logik aus deinem alten Patch, jetzt als wiederverwendbare Methode.
        /// </summary>
        /// <summary>
        /// Führt den Teleport eines Charakters hinter den Spieler aus.
        /// Dies ist eine 1:1-Kopie der funktionierenden Logik aus dem DistanceTeleportPatch.
        /// </summary>
        /// <param name="characterToTeleport">Das Tier, das teleportiert werden soll.</param>
        /// <param name="followTarget">Der Spieler, dem das Tier folgt.</param>
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

            Vector3 positionBehind = -forwardVec * distanceBehind;
            Vector3 positionWithSideOffset = rightVec * sideOffset;
            Vector3 targetPosition = playerPosition + positionBehind + positionWithSideOffset;

            // Boden-Höhenanpassung
            if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out RaycastHit hitInfo, 10f, groundLayerMask))
            {
                targetPosition.y = hitInfo.point.y + 1f;
            }
            else
            {
                targetPosition.y = playerPosition.y; // Fallback, falls kein Boden gefunden wird
            }

            // Finale Rotation und Teleport-Aktion
            Quaternion targetRotation = Quaternion.LookRotation(forwardVec);

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
            BetterTamesPlugin.LogIfDebug($"Teleported {characterToTeleport.m_name} to {targetPosition}. Sending RPC.", DebugFeature.TeleportFollow);
            ZPackage package = new ZPackage();
            package.Write(targetPosition);
            package.Write(targetRotation);
            string zdoIDString = $"{zdo.m_uid.UserID}:{zdo.m_uid.ID}";
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BT_TeleportSync", new object[] { zdoIDString, package });

            // --- Ende des extrahierten Codes ---
        }

        // Diese Methode wird nicht mehr direkt aufgerufen, kann aber als Helfer bleiben
        public static string GetPrefabName(Character c)
        {
            return c != null ? c.name.Replace("(Clone)", "") : "UnknownCharacter";
        }

        // Token: 0x0600004A RID: 74 RVA: 0x00003764 File Offset: 0x00001964
        public static float GetPetHeight(Character character)
        {
            if (character == null)
            {
                BetterTamesPlugin.LogIfDebug("GetPetHeight: Pet nicht gefunden. Fallback auf Höhe 1f.", DebugFeature.TeleportFollow);
                return 1f;
            }
            CapsuleCollider component = character.GetComponent<CapsuleCollider>();
            if (component != null)
            {
                float num = component.height * character.transform.localScale.y;
                BetterTamesPlugin.LogIfDebug(string.Format("GetPetHeight für {0}: CapsuleCollider gefunden. Höhe = {1:F2} (capsule.height={2:F2} * scale.y={3:F2})", new object[]
                {
                    DistanceTeleportLogic.GetPrefabName(character),
                    num,
                    component.height,
                    character.transform.localScale.y
                }), DebugFeature.TeleportFollow);
                return num;
            }
            Collider component2 = character.GetComponent<Collider>();
            if (component2 != null)
            {
                float y = component2.bounds.size.y;
                BetterTamesPlugin.LogIfDebug(string.Format("GetPetHeight für {0}: Fallback auf generischen Collider ({1}) gefunden. Höhe (bounds.size.y) = {2:F2}", DistanceTeleportLogic.GetPrefabName(character), component2.GetType().Name, y), DebugFeature.TeleportFollow);
                return y;
            }
            BetterTamesPlugin.LogIfDebug("GetPetHeight für " + DistanceTeleportLogic.GetPrefabName(character) + ": Keinen Collider gefunden. Fallback auf Höhe 1f.", DebugFeature.TeleportFollow);
            return 1f;
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


        // Token: 0x0600004B RID: 75 RVA: 0x00003874 File Offset: 0x00001A74
        public static void TeleportPetToActualPosition(Character petCharacter, Vector3 targetPosition, Quaternion targetRotation, Player teleportingPlayer)
        {
            if (petCharacter == null)
            {
                return;
            }
            ZNetView component = petCharacter.GetComponent<ZNetView>();
            if (component == null || !component.IsValid())
            {
                return;
            }
            string prefabName = DistanceTeleportLogic.GetPrefabName(petCharacter);
            BetterTamesPlugin.LogIfDebug(string.Format("TeleportPetToActualPosition: Setting {0} to {1} with rotation {2}", prefabName, targetPosition, targetRotation.eulerAngles), DebugFeature.TeleportFollow);
            petCharacter.transform.position = targetPosition;
            petCharacter.transform.rotation = targetRotation;
            Rigidbody component2 = petCharacter.GetComponent<Rigidbody>();
            if (component2 != null)
            {
                if (!component2.isKinematic)
                {
                    BetterTamesPlugin.LogIfDebug(prefabName + " ist nicht kinematisch, Velocity wird zurückgesetzt.", DebugFeature.TeleportFollow);
                    component2.linearVelocity = Vector3.zero;
                    component2.angularVelocity = Vector3.zero;
                }
                else
                {
                    BetterTamesPlugin.LogIfDebug(prefabName + " ist kinematisch, Velocity-Änderungen übersprungen.", DebugFeature.TeleportFollow);
                }
            }
            Tameable component3 = petCharacter.GetComponent<Tameable>();
            if (component3 != null)
            {
                component3.m_unsummonDistance = 0f;
            }
            MonsterAI component4 = petCharacter.GetComponent<MonsterAI>();
            if (component4 != null)
            {
                component4.StopMoving();
                if (teleportingPlayer != null)
                {
                    component4.SetFollowTarget(teleportingPlayer.gameObject);
                }
            }
            ZDO zdo = component.GetZDO();
            zdo.SetPosition(targetPosition);
            zdo.SetRotation(targetRotation);
            ZPackage zpackage = new ZPackage();
            zpackage.Write(targetPosition);
            zpackage.Write(targetRotation);
            string text = string.Format("{0}:{1}", zdo.m_uid.UserID, zdo.m_uid.ID);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BT_TeleportSync", new object[]
            {
                text,
                zpackage
            });
            BetterTamesPlugin.LogIfDebug("TeleportPetToActualPosition: RPC_TELEPORT_SYNC sent for " + prefabName + ".", DebugFeature.TeleportFollow);
        }

        // Token: 0x04000029 RID: 41
        public const float PET_PLACEMENT_BUFFER = 0.15f;
    }
}
