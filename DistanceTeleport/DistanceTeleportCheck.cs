using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BetterTames; // NEU: Für BetterTamesPlugin und DebugFeature

namespace BetterTames.DistanceTeleport
{
    public static class PlayerPetMonitor
    {
        // Wir verwenden die Konstante aus der Logic-Klasse
        private const float CheckInterval = DistanceTeleportLogic.TELEPORT_CHECK_INTERVAL;

        /// <summary>
        /// Public static coroutine that can be started on any MonoBehaviour (e.g. Player.m_localPlayer).
        /// </summary>
        public static IEnumerator MonitorRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(CheckInterval);

                if (Player.m_localPlayer == null)
                {
                    BetterTamesPlugin.LogIfDebug("PlayerPetMonitor: Player.m_localPlayer == null, skipping this tick.", DebugFeature.TeleportFollow);
                    continue;
                }

                BetterTamesPlugin.LogIfDebug("PlayerPetMonitor: Starting scan for owned following pets.", DebugFeature.TeleportFollow);

                List<Character> allCharacters = Character.GetAllCharacters();
                if (allCharacters == null || allCharacters.Count == 0)
                {
                    BetterTamesPlugin.LogIfDebug("PlayerPetMonitor: No characters found in the world. Skipping scan.", DebugFeature.TeleportFollow);
                    continue;
                }

                List<Character> followingPets = new List<Character>();

                foreach (Character character in allCharacters)
                {
                    if (character == null || !character.IsTamed()) continue;

                    ZNetView nview = character.GetComponent<ZNetView>();
                    if (nview == null) continue;

                    // Only the owner should run teleport logic for this pet
                    if (!nview.IsOwner()) continue;

                    MonsterAI ai = character.GetComponent<MonsterAI>();
                    if (ai != null && ai.GetFollowTarget() != null)
                    {
                        followingPets.Add(character);
                    }
                }

                BetterTamesPlugin.LogIfDebug($"PlayerPetMonitor: Found {followingPets.Count} owned pets with follow targets.", DebugFeature.TeleportFollow);

                foreach (Character pet in followingPets)
                {
                    try
                    {
                        bool teleported = DistanceTeleportLogic.CheckDistanceAndTeleport(pet);
                        if (teleported)
                        {
                            BetterTamesPlugin.LogIfDebug($"PlayerPetMonitor: Teleport triggered for {pet.m_name}.", DebugFeature.TeleportFollow);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        BetterTamesPlugin.LogIfDebug($"PlayerPetMonitor: Exception while checking/teleporting pet {pet?.m_name}: {ex}", DebugFeature.TeleportFollow);
                    }
                }
            }
                        }
    }
}