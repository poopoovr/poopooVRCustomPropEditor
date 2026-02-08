using poopooVRCustomPropEditor.Features;
using HarmonyLib;
using Photon.Pun;
using System;
using UnityEngine;

namespace poopooVRCustomPropEditor.Patches
{
    [HarmonyPatch(typeof(VRRig))]
    [HarmonyPatch("IUserCosmeticsCallback.OnGetUserCosmetics", MethodType.Normal)]
    public static class PlayerCosmeticsLoadedPatch
    {
        private static void Postfix(VRRig __instance)
        {
            try
            {
                if (__instance == null) return;

                Plugin.Instance?.PlayerInfo?.OnPlayerCosmeticsLoaded(__instance);

                Debug.Log($"[PlayerPatches] Cosmetics loaded for: {__instance.OwningNetPlayer?.NickName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPatches] Error in PlayerCosmeticsLoadedPatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(VRRigCache), nameof(VRRigCache.RemoveRigFromGorillaParent))]
    public static class PlayerRigRemovedPatch
    {
        private static void Postfix(NetPlayer player, VRRig vrrig)
        {
            try
            {
                if (vrrig == null) return;

                Plugin.Instance?.PlayerInfo?.OnPlayerLeft(vrrig);
                Plugin.Instance?.ModChecker?.OnPlayerLeft(vrrig);

                Debug.Log($"[PlayerPatches] Player left: {player?.NickName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPatches] Error in PlayerRigRemovedPatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(VRRig), nameof(VRRig.SerializeReadShared))]
    public static class PlayerSerializePatch
    {
        private static void Postfix(VRRig __instance, InputStruct data)
        {
            try
            {
                if (__instance == null || Plugin.Instance?.PlayerInfo == null)
                    return;

                if (__instance.velocityHistoryList != null && __instance.velocityHistoryList.Count > 0)
                {
                    double ping = Math.Abs((__instance.velocityHistoryList[0].time - PhotonNetwork.Time) * 1000);
                    int safePing = (int)Math.Clamp(Math.Round(ping), 0, int.MaxValue);

                    Plugin.Instance.PlayerInfo.PlayerPing[__instance] = safePing;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPatches] Error in PlayerSerializePatch: {ex.Message}");
            }
        }
    }
}
