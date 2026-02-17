using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class MainUnitManager
{
    [Server]
    public void LoadFromSaveServer(SaveSystem.SaveData data)
    {
        if (data == null) return;

        ApplyCountriesFromSave(data);
        RebuildUnitsFromSave(data);
    }

    [Server]
    private void ApplyCountriesFromSave(SaveSystem.SaveData data)
    {
        for (int i = 0; i < data.countries.Count; i++)
        {
            var s = data.countries[i];
            if (string.IsNullOrEmpty(s.tag)) continue;

            var c = FindCountryByTag(s.tag);
            if (c == null) continue;

            c.ownerID = s.ownerID;
        }
    }

    [Server]
    private void RebuildUnitsFromSave(SaveSystem.SaveData data)
    {
        for (int i = allUnits.Count - 1; i >= 0; i--)
        {
            var u = allUnits[i];
            if (u == null) continue;
            NetworkServer.Destroy(u.gameObject);
        }
        allUnits.Clear();

        for (int i = 0; i < data.units.Count; i++)
        {
            var s = data.units[i];
            GameObject prefab =
                s.unitType == (int)UnitType.Boat ? boatUnitPrefab :
                s.unitType == (int)UnitType.Plane ? planeUnitPrefab :   
                landUnitPrefab;

            if (prefab == null) continue;

            var obj = Instantiate(prefab, s.position, Quaternion.identity);
            var unit = obj.GetComponent<MainUnit>();
            if (unit == null)
            {
                Destroy(obj);
                continue;
            }

            unit.ownerID = s.ownerID;
            unit.unitType = (UnitType)s.unitType;
            unit.currentCountry = s.currentCountry;

            NetworkServer.Spawn(obj);
            allUnits.Add(unit);

            unit.RpcInitialize(unit.ownerID, unit.playerColor, unit.unitType);
        }
    }
}