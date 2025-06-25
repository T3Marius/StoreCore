using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using static StoreCore.WeaponModels;

namespace StoreCore;

public static class Model
{
    public static bool Equip(CCSPlayerController player, string weaponId)
    {
        if (!player.IsValid || !player.PawnIsAlive)
            return false;

        var weaponModel = GetWeaponModelById(weaponId);
        if (weaponModel == null)
            return false;

        return HandleEquip(player, weaponModel.Model, true);
    }

    public static bool Unequip(CCSPlayerController player, string weaponId)
    {
        if (!player.IsValid || !player.PawnIsAlive)
            return false;

        var weaponModel = GetWeaponModelById(weaponId);
        if (weaponModel == null)
            return false;

        return HandleEquip(player, weaponModel.Model, false);
    }

    private static WeaponModel_Item? GetWeaponModelById(string weaponId)
    {
        return Instance.Config.WeaponModels.Values.FirstOrDefault(w => w.Id == weaponId);
    }

    public static bool HandleEquip(CCSPlayerController player, string modelName, bool isEquip)
    {
        if (!player.PawnIsAlive)
            return false;

        var weaponpart = modelName.Split(':');
        if (weaponpart.Length < 2 || weaponpart.Length > 3)
            return false;

        string weaponName = weaponpart[0];
        string weaponModel = weaponpart[1];
        string worldModel = weaponpart.Length == 3 ? weaponpart[2] : weaponpart[1];

        CBasePlayerWeapon? weapon = GetPlayerWeapon(player, weaponName);

        if (weapon != null)
        {
            bool isActiveWeapon = weapon == player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

            if (isEquip)
                UpdateModel(player, weapon, weaponModel, worldModel, isActiveWeapon);
            else
                ResetWeapon(player, weapon, isActiveWeapon);

            return true;
        }

        return false;
    }

    public static void UpdateModel(CCSPlayerController player, CBasePlayerWeapon weapon, string model, string worldmodel, bool update)
    {
        weapon.Globalname = $"{GetViewModel(player)},{model}";
        weapon.SetModel(worldmodel);

        if (update)
            SetViewModel(player, model);
    }

    public static void ResetWeapon(CCSPlayerController player, CBasePlayerWeapon weapon, bool update)
    {
        string globalname = weapon.Globalname;

        if (string.IsNullOrEmpty(globalname))
            return;

        string[] globalnamedata = globalname.Split(',');
        if (globalnamedata.Length < 2)
            return;

        weapon.Globalname = string.Empty;
        weapon.SetModel(globalnamedata[0]);

        if (update)
            SetViewModel(player, globalnamedata[0]);
    }

    private static CBasePlayerWeapon? GetPlayerWeapon(CCSPlayerController player, string weaponName)
    {
        CPlayer_WeaponServices? weaponServices = player.PlayerPawn?.Value?.WeaponServices;

        if (weaponServices == null)
            return null;

        CBasePlayerWeapon? activeWeapon = weaponServices.ActiveWeapon?.Value;

        if (activeWeapon != null && GetDesignerName(activeWeapon) == weaponName)
            return activeWeapon;

        return weaponServices.MyWeapons.Where(p => p.Value != null && GetDesignerName(p.Value) == weaponName).FirstOrDefault()?.Value;
    }

    public static string GetDesignerName(CBasePlayerWeapon weapon)
    {
        string weaponDesignerName = weapon.DesignerName;
        ushort weaponIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

        weaponDesignerName = (weaponDesignerName, weaponIndex) switch
        {
            var (name, _) when name.Contains("bayonet") => "weapon_knife",
            ("weapon_m4a1", 60) => "weapon_m4a1_silencer",
            ("weapon_hkp2000", 61) => "weapon_usp_silencer",
            ("weapon_deagle", 64) => "weapon_revolver",
            ("weapon_mp7", 23) => "weapon_mp5sd",
            _ => weaponDesignerName
        };

        return weaponDesignerName;
    }

    public static unsafe string GetViewModel(CCSPlayerController player)
    {
        var viewModel = GetPlayerViewModel(player)?.VMName ?? string.Empty;
        return viewModel;
    }

    public static unsafe void SetViewModel(CCSPlayerController player, string model)
    {
        GetPlayerViewModel(player)?.SetModel(model);
    }

    private static unsafe CBaseViewModel? GetPlayerViewModel(CCSPlayerController player)
    {
        nint? handle = player.PlayerPawn.Value?.ViewModelServices?.Handle;

        if (handle == null || !handle.HasValue)
            return null;

        CCSPlayer_ViewModelServices viewModelServices = new(handle.Value);

        nint ptr = viewModelServices.Handle + Schema.GetSchemaOffset("CCSPlayer_ViewModelServices", "m_hViewModel");
        Span<nint> viewModels = MemoryMarshal.CreateSpan(ref ptr, 3);

        CHandle<CBaseViewModel> viewModel = new(viewModels[0]);

        return viewModel.Value;
    }
}