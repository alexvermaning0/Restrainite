using System.Reflection;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using HarmonyLib;
using Restrainite.RestrictionTypes.Base;
using SkyFrost.Base;

namespace Restrainite.Patches;

[HarmonyPatch]
internal static class ForceEquipAvatar
{
    // Set while WE equip the forced avatar, so the Equip block below lets our own equip through.
    [ThreadStatic] private static bool _isForceEquipping;

    // The forced avatar slot we currently maintain; re-equipped if it gets destroyed.
    private static Slot? _watchedAvatarSlot;

    public static void Initialize()
    {
        Restrictions.ForceEquipAvatar.OnChanged += OnChanged;
        UserRootInjector.OnUserRootInitialized += OnUserRootInitialized;
    }

    private static void OnChanged(IRestriction _)
    {
        TryForceEquip();
    }

    private static void OnUserRootInitialized(UserRoot userRoot)
    {
        if (userRoot.World == Engine.Current.WorldManager.FocusedWorld)
            userRoot.RunInUpdates(1, TryForceEquip);
    }

    private static void TryForceEquip()
    {
        if (!Restrictions.ForceEquipAvatar.IsRestricted) return;

        // Already maintaining a live forced avatar; nothing to do.
        if (_watchedAvatarSlot is { IsDisposed: false, IsDestroyed: false }) return;

        var url = Restrictions.ForceEquipAvatar.AvatarUrl.Value.FirstOrDefault();
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var world = Engine.Current?.WorldManager?.FocusedWorld;
        var localUser = world?.LocalUser;
        if (localUser == null) return;

        localUser.Root?.RunSynchronously(() => StartEquipTask(world!, uri));
    }

    private static void StartEquipTask(World world, Uri uri)
    {
        var spawnSlot = world.RootSlot.LocalUserSpace.AddSlot("ForceEquipAvatar");
        spawnSlot.StartTask(async () =>
        {
            var recordResult = await world.Engine.Cloud.Records.GetRecordCached<Record>(uri, null);
            if (recordResult.IsError)
            {
                spawnSlot.Destroy();
                return;
            }

            await spawnSlot.LoadObjectAsync(recordResult.Entity);
            var inventoryItem = spawnSlot.GetComponent<InventoryItem>();
            var avatarSlot = (inventoryItem != null ? inventoryItem.Unpack() : null) ?? spawnSlot;
            world.RunInUpdates(1, () => EquipForcedAvatar(world, avatarSlot));
        });
    }

    private static void EquipForcedAvatar(World world, Slot avatarSlot)
    {
        if (!Restrictions.ForceEquipAvatar.IsRestricted)
        {
            avatarSlot.Destroy();
            return;
        }

        var localUser = world.LocalUser;
        var avatarManager = localUser?.Root?.GetRegisteredComponent<AvatarManager>();
        if (avatarManager == null)
        {
            avatarSlot.Destroy();
            return;
        }

        if (_watchedAvatarSlot != null)
        {
            _watchedAvatarSlot.Disposing -= OnAvatarSlotDisposing;
            _watchedAvatarSlot = null;
        }

        var rootSlot = localUser!.Root!.Slot;
        avatarSlot.GlobalPosition = rootSlot.GlobalPosition;
        avatarSlot.GlobalRotation = rootSlot.GlobalRotation;

        _isForceEquipping = true;
        try
        {
            avatarManager.ClearEquipped();
            avatarManager.Equip(avatarSlot, true, false, false);
        }
        finally
        {
            _isForceEquipping = false;
        }

        _watchedAvatarSlot = avatarSlot;
        avatarSlot.Disposing += OnAvatarSlotDisposing;
    }

    private static void OnAvatarSlotDisposing(Worker _)
    {
        _watchedAvatarSlot = null;
        if (Restrictions.ForceEquipAvatar.IsRestricted) TryForceEquip();
    }

    // While forced, block the user from equipping any other avatar. Destroy the incoming
    // avatar slot and report failure, unless the equip is our own forced one.
    [HarmonyPatch]
    private static class EquipPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(AvatarManager)).Where(m => m.Name == "Equip");
        }

        private static bool Prefix(Slot __0, ref bool __result)
        {
            if (!Restrictions.ForceEquipAvatar.IsRestricted) return true;
            if (_isForceEquipping) return true;

            __0?.Destroy();
            __result = false;
            return false;
        }
    }

    // Prevent the game from auto-filling emptied avatar slots for the local user while forced.
    [HarmonyPatch]
    private static class FillEmptySlotsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var method = AccessTools.Method(typeof(AvatarManager), "FillEmptySlots");
            return method != null ? [method] : [];
        }

        private static bool Prefix(AvatarManager __instance)
        {
            if (!Restrictions.ForceEquipAvatar.IsRestricted) return true;

            var localManager = Engine.Current?.WorldManager?.FocusedWorld?.LocalUser?.Root?
                .GetRegisteredComponent<AvatarManager>();
            // Only suppress for the local user's own avatar manager.
            return __instance != localManager;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldPermissionsExtensoins), "CanSwapAvatar")]
    private static bool WorldPermissionsExtensoins_CanSwapAvatar_Prefix(ref bool __result)
    {
        if (!Restrictions.ForceEquipAvatar.IsRestricted) return true;
        __result = false;
        return false;
    }
}
