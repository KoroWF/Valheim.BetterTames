//using System;
//using System.Collections.Generic;
//using BepInEx.Configuration;
//using HarmonyLib;
//using UnityEngine;
//using BetterTames.ConfigSynchronization;


//namespace BetterTames.DistanceTeleport
//{
//    // Token: 0x0200000D RID: 13
//    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
//    public static class DistanceTeleportPatch
//    {

//        // Token: 0x0600004F RID: 79 RVA: 0x00003C70 File Offset: 0x00001E70
//        [HarmonyPostfix]
//        public static void Postfix(MonsterAI __instance, float dt)
//        {
//            ConfigSync configInstance = BetterTamesPlugin.ConfigInstance;
//            bool flag;
//            if (configInstance == null)
//            {
//                flag = true;
//            }
//            else
//            {
//                ConfigSync.TamesConfig tames = configInstance.Tames;
//                bool? flag2;
//                if (tames == null)
//                {
//                    flag2 = null;
//                }
//                else
//                {
//                    ConfigEntry<bool> teleportFollowEnabled = tames.TeleportFollowEnabled;
//                    flag2 = ((teleportFollowEnabled != null) ? new bool?(teleportFollowEnabled.Value) : null);
//                }
//                bool? flag3 = flag2;
//                flag = !flag3.GetValueOrDefault();
//            }
//            if (flag || ZNet.instance == null)
//            {
//                return;
//            }
//            ZNetView component = __instance.GetComponent<ZNetView>();
//            if (component == null || !component.IsValid())
//            {
//                return;
//            }
//            ZDO zdo = component.GetZDO();
//            if (zdo == null)
//            {
//                return;
//            }
//            Character component2 = __instance.GetComponent<Character>();
//            if (component2 == null || !component2.IsTamed())
//            {
//                return;
//            }
//            GameObject followTarget = __instance.GetFollowTarget();
//            if (followTarget == null)
//            {
//                return;
//            }
//            Vector3 position = component2.transform.position;
//            Vector3 position2 = followTarget.transform.position;
//            if (component.IsOwner())
//            {
//                float num = Vector3.Distance(position, position2);
//                float num2 = 5f;
//                Character targetCreature = __instance.GetTargetCreature();
//                StaticTarget staticTarget = __instance.GetStaticTarget();
//                bool flag4 = (targetCreature != null && BaseAI.IsEnemy(component2, targetCreature)) || staticTarget != null;
//                if (num < num2 && !flag4)
//                {
//                    __instance.StopMoving();
//                }
//            }
//            if (!component.IsOwner())
//            {
//                return;
//            }
//            float time = Time.time;
//            float num3;
//            if (!DistanceTeleportPatch.nextTeleportCheckTime.TryGetValue(zdo.m_uid, out num3))
//            {
//                num3 = 0f;
//            }
//            if (time >= num3)
//            {
//                DistanceTeleportPatch.nextTeleportCheckTime[zdo.m_uid] = time + 3f;
//                float num4 = Mathf.Max(BetterTamesPlugin.ConfigInstance.Tames.TeleportOnDistanceMaxRange.Value, 10f);
//                float sqrMagnitude = (position - position2).sqrMagnitude;
//                float num5 = num4 * num4;
//                if (sqrMagnitude > num5)
//                {
//                    BetterTamesPlugin.LogIfDebug(string.Format("DistanceSqr {0:F1} > {1:F1}. Attempting teleport for {2}.", sqrMagnitude, num5, component2.m_name), DebugFeature.TeleportFollow);
//                    Vector3 position3 = followTarget.transform.position;
//                    if (position3.y > 1000f)
//                    {
//                        BetterTamesPlugin.LogIfDebug(string.Format("Player Y position {0:F1} is > 2000. Preventing pet teleport for {1} to avoid dungeon issue.", position3.y, component2.m_name), DebugFeature.TeleportFollow);
//                        return;
//                    }
//                    Quaternion rotation = followTarget.transform.rotation;
//                    Vector3 vector = rotation * Vector3.forward;
//                    Vector3 a = rotation * Vector3.right;
//                    float num6 = 1f;
//                    CapsuleCollider component3 = component2.GetComponent<CapsuleCollider>();
//                    if (component3 != null)
//                    {
//                        num6 = component3.radius * Mathf.Max(component2.transform.localScale.x, component2.transform.localScale.z);
//                    }
//                    else
//                    {
//                        Collider component4 = component2.GetComponent<Collider>();
//                        if (component4 != null)
//                        {
//                            num6 = Mathf.Max(component4.bounds.extents.x, component4.bounds.extents.z);
//                        }
//                    }
//                    float num7 = Mathf.Max(10f, num6 + 0.5f);
//                    float maxInclusive = num7 + 5f;
//                    float num8 = Mathf.Max(5f, num6 * 1.5f);
//                    float d = UnityEngine.Random.Range(num7, maxInclusive);
//                    float d2 = UnityEngine.Random.Range(-num8 / 2f, num8 / 2f);
//                    Vector3 a2 = -vector * d;
//                    Vector3 b = a * d2;
//                    Vector3 b2 = a2 + b;
//                    Vector3 vector2 = position3 + b2;
//                    RaycastHit raycastHit;
//                    if (Physics.Raycast(vector2 + Vector3.up * 5f, Vector3.down, out raycastHit, 10f, DistanceTeleportPatch.groundLayerMask))
//                    {
//                        vector2.y = raycastHit.point.y + 1f;
//                    }
//                    else
//                    {
//                        vector2.y = position3.y;
//                        BetterTamesPlugin.LogIfDebug("No ground found via Raycast for auto-teleport of " + component2.m_name + ", using target Y position.", DebugFeature.TeleportFollow);
//                    }
//                    Quaternion quaternion = Quaternion.LookRotation(vector);
//                    component2.transform.position = vector2;
//                    component2.transform.rotation = quaternion;
//                    zdo.SetPosition(vector2);
//                    zdo.SetRotation(quaternion);
//                    Rigidbody component5 = component2.GetComponent<Rigidbody>();
//                    if (component5 != null)
//                    {
//                        component5.WakeUp();
//                    }
//                    BetterTamesPlugin.LogIfDebug(string.Format("Teleported {0} to {1} (behind followTarget). Sending RPC.", component2.m_name, vector2), DebugFeature.TeleportFollow);
//                    ZPackage zpackage = new ZPackage();
//                    zpackage.Write(vector2);
//                    zpackage.Write(quaternion);
//                    string text = string.Format("{0}:{1}", zdo.m_uid.UserID, zdo.m_uid.ID);
//                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BT_TeleportSync", new object[]
//                    {
//                        text,
//                        zpackage
//                    });
//                }
//            }
//        }

//        // Token: 0x0400002A RID: 42
//        public static readonly int groundLayerMask = LayerMask.GetMask(new string[]
//        {
//            "Default",
//            "static_solid",
//            "Default_small",
//            "piece",
//            "terrain",
//            "blocker",
//            "vehicle"
//        });

//        // Token: 0x0400002B RID: 43
//        private static readonly Dictionary<ZDOID, float> nextTeleportCheckTime = new Dictionary<ZDOID, float>();

//        // Token: 0x0400002C RID: 44
//        private const float TELEPORT_CHECK_INTERVAL = 3f;
//    }
//}
