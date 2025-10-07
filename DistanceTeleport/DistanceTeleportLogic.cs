
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames
{
    public static class DistanceTeleportLogic
    {
        private static readonly int groundLayerMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker", "vehicle");

        public static void ExecuteTeleportBehindPlayer(Character characterToTeleport, GameObject followTarget)
        {
            ZNetView nview = characterToTeleport.GetComponent<ZNetView>();
            ZDO zdo = nview?.GetZDO();

            Vector3 playerPosition = followTarget.transform.position;
            Quaternion playerRotation = followTarget.transform.rotation;

            BetterTamesPlugin.LogIfDebug($"Attempting teleport for {characterToTeleport.m_name}.", DebugFeature.TeleportFollow);

            if (playerPosition.y > 1000f)
            {
                BetterTamesPlugin.LogIfDebug($"Player Y position {playerPosition.y:F1} is > 1000. Preventing pet teleport.", DebugFeature.TeleportFollow);
                return;
            }

            // --- KORREKTUR START ---
            // Berechne den Vektor HINTER den Spieler
            Vector3 behindPlayerVector = playerRotation * -Vector3.forward;

            // Berechne die Zielposition 10m hinter dem Spieler
            float teleportDistance = 10f;
            Vector3 targetPosition = playerPosition + behindPlayerVector * teleportDistance;
            // --- KORREKTUR ENDE ---

            RaycastHit raycastHit;
            if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out raycastHit, 10f, groundLayerMask))
            {
                targetPosition.y = raycastHit.point.y; // Direkt auf den Boden setzen
            }
            else
            {
                targetPosition.y = playerPosition.y;
                BetterTamesPlugin.LogIfDebug("No ground found via Raycast for teleport of " + characterToTeleport.m_name + ", using player Y position.", DebugFeature.TeleportFollow);
            }

            // Drehe das Tier in Richtung des Spielers
            Quaternion targetRotation = Quaternion.LookRotation(-behindPlayerVector);

            characterToTeleport.transform.position = targetPosition;
            characterToTeleport.transform.rotation = targetRotation;
            if (zdo != null)
            {
                zdo.SetPosition(targetPosition);
                zdo.SetRotation(targetRotation);
            }

            Rigidbody rb = characterToTeleport.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.WakeUp();
            }
            BetterTamesPlugin.LogIfDebug($"[ZDO] Teleported {characterToTeleport.m_name} to {targetPosition}.", DebugFeature.TeleportFollow);
        }

        public const float PET_PLACEMENT_BUFFER = 0.15f;
    }
}