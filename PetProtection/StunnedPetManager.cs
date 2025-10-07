using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.PetProtection
{
    // Ist jetzt eine statische Klasse, erbt nicht mehr von MonoBehaviour
    public static class StunnedPetManager
    {
        // Das Dictionary ist jetzt statisch
        private static Dictionary<ZDOID, float> stunnedPets = new Dictionary<ZDOID, float>(); // ZDOID -> RevivalTime

        // Awake() und Instance werden nicht mehr benötigt

        public static void AddStunnedPet(ZDOID petId, float revivalTimestamp)
        {
            // Die IsServer()-Prüfung ist hier immer noch wichtig
            if (!ZNet.instance.IsServer()) return;
            if (!stunnedPets.ContainsKey(petId))
            {
                stunnedPets[petId] = revivalTimestamp;
                BetterTamesPlugin.LogIfDebug($"Server: Tracking stunned pet {petId} until {revivalTimestamp}", DebugFeature.PetProtection);
            }
        }

        // Die alte Update-Methode, jetzt statisch und mit neuem Namen, um Verwechslungen zu vermeiden
        public static void UpdateRevivalChecks()
        {
            if (!ZNet.instance.IsServer()) return;

            // Die Logik hier drin ist 1:1 dieselbe wie vorher
            if (stunnedPets.Count == 0) return;

            List<ZDOID> toRevive = new List<ZDOID>();
            double currentTime = ZNet.instance.GetTimeSeconds();

            foreach (var kvp in stunnedPets)
            {
                if (currentTime >= kvp.Value)
                {
                    toRevive.Add(kvp.Key);
                }
            }

            if (toRevive.Count > 0)
            {
                BetterTamesPlugin.LogIfDebug($"Found {toRevive.Count} pets to revive.", DebugFeature.PetProtection);
            }

            foreach (ZDOID petId in toRevive)
            {
                stunnedPets.Remove(petId);
                ZDO zdo = ZDOMan.instance.GetZDO(petId);
                if (zdo != null)
                {
                    GameObject petGo = ZNetScene.instance.FindInstance(zdo)?.gameObject;
                    if (petGo != null)
                    {
                        Character petCharacter = petGo.GetComponent<Character>();
                        if (petCharacter != null)
                        {
                            float maxHP = petCharacter.GetMaxHealth();
                            int healprecentInt = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionHealPercentage.Value;
                            float healprecent = (float)healprecentInt;
                            float healPct = healprecent;
                            float healthToRestore = Mathf.Max(maxHP * 0.01f, maxHP * (healPct / 100f));
                            zdo.Set(ZDOVars.s_health, healthToRestore);
                            BetterTamesPlugin.LogIfDebug($"Server: Healing pet {petId} to {healthToRestore} HP.", DebugFeature.PetProtection);

                            if (zdo.GetBool("following", false))
                            {
                                long ownerID = zdo.GetLong("owner", 0L);
                                Player owner = Player.GetPlayer(ownerID);
                                if (owner != null)
                                {

                                    BetterTamesPlugin.LogIfDebug($"Server: Pet {petId} is on follow, teleporting to owner '{owner.GetPlayerName()}'.", DebugFeature.PetProtection);
                                    DistanceTeleportLogic.ExecuteTeleportBehindPlayer(petCharacter, owner.gameObject);

                                }
                            }
                        }
                    }

                    zdo.Set("BT_Stunned", false);
                    BetterTamesPlugin.LogIfDebug($"[ZDO Listener] Pet exited stunned state visuals.", DebugFeature.PetProtection);
                    BetterTamesPlugin.LogIfDebug($"Server: Revived pet {petId}.", DebugFeature.PetProtection);
                }
            }
        }
    }
}