using UnityEngine;
using System.Collections.Generic;

namespace BetterTames.DistanceTeleport
{
    public class PetDistanceChecker : MonoBehaviour
    {


        private float nextCheckTime = 0f;

        void Update()
        {

            if (Time.time < nextCheckTime) return;
            BetterTamesPlugin.LogIfDebug($"Monobehavior is checking for Tame Teleports.", DebugFeature.Initialization);

            nextCheckTime = Time.time + 5f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            foreach (var character in Character.GetAllCharacters())
            {
                if (!character.IsTamed()) continue;
                var ai = character.GetComponent<MonsterAI>();
                if (ai == null) continue;
                var zview = character.GetComponent<ZNetView>();
                if (zview == null || !zview.IsOwner()) continue;

                var followTarget = ai.GetFollowTarget();
                if (followTarget == null || followTarget != player.gameObject) continue;

                // Prüfe Distanz wie im Patch
                float maxDist = Mathf.Max(BetterTamesPlugin.ConfigInstance.Tames.TeleportOnDistanceMaxRange.Value);
                float sqrDist = (character.transform.position - player.transform.position).sqrMagnitude;
                if (sqrDist > maxDist * maxDist)
                {
                    // Nutze die zentrale Teleport-Methode
                    DistanceTeleportLogic.ExecuteTeleportBehindPlayer(character, player.gameObject);
                }
            }
        }
    }
}