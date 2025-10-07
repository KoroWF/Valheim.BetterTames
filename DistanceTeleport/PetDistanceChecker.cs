using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames
{
    public class PetDistanceChecker : MonoBehaviour
    {
        private Player localPlayer;
        private const float CHECK_INTERVAL = 3.0f; // Prüfe nur alle 3 Sekunden

        private void Awake()
        {
            localPlayer = GetComponent<Player>();
            BetterTamesPlugin.LogIfDebug($"MonoBehaviour on Player is loaded!.", DebugFeature.TeleportFollow);
        }

        private void OnEnable()
        {
            StartCoroutine(CheckPetDistanceLoop());
        }

        private IEnumerator CheckPetDistanceLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(CHECK_INTERVAL);

                // --- KORRIGIERTE ZEILE ---
                // Prüft, ob der Spieler existiert und ob wir entweder der Server sind oder ein verbundener Client.
                if (localPlayer == null || (!ZNet.instance.IsServer() && ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected))
                {
                    continue; // Nichts tun, wenn die Bedingungen nicht erfüllt sind.
                }

                int maxRangeInt = BetterTamesPlugin.ConfigInstance.Tames.TeleportOnDistanceMaxRange.Value;
                float maxRangeFloat = (float)maxRangeInt;

                List<Character> allPets = Character.GetAllCharacters();
                foreach (Character pet in allPets)
                {
                    if (pet.IsTamed() && pet.GetComponent<MonsterAI>()?.GetFollowTarget() == localPlayer.gameObject)
                    {
                        float distance = Vector3.Distance(pet.transform.position, localPlayer.transform.position);

                        if (distance > maxRangeFloat)
                        {
                            ZDO zdo = pet.GetComponent<ZNetView>()?.GetZDO();
                            if (zdo != null && !zdo.GetBool("BT_Stunned", false))
                            {
                                BetterTamesPlugin.LogIfDebug($"Pet {pet.m_name} is too far ({distance:F1}m > {maxRangeFloat}m). Teleporting.", DebugFeature.TeleportFollow);
                                DistanceTeleportLogic.ExecuteTeleportBehindPlayer(pet, localPlayer.gameObject);
                            }
                        }
                    }
                }
            }
        }
    }
}