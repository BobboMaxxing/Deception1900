using System;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEngine;

public static class SaveSystem
{
    [Serializable]
    public class SaveData
    {
        public string sceneName;
        public List<CountryState> countries = new List<CountryState>();
        public List<UnitState> units = new List<UnitState>();
    }

    [Serializable]
    public class CountryState
    {
        public string tag;
        public int ownerID;
    }

    [Serializable]
    public class UnitState
    {
        public int ownerID;
        public int unitType;
        public string currentCountry;
        public Vector3 position;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "save_latest.json");

    public static bool HasLatestSave()
    {
        return File.Exists(FilePath);
    }

    public static void DeleteLatest()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    public static void SaveLatestServer()
    {
        if (!NetworkServer.active) return;

        var mgr = MainUnitManager.Instance;
        if (mgr == null) return;

        SaveData data = new SaveData();
        data.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        var countries = UnityEngine.Object.FindObjectsOfType<Country>();
        for (int i = 0; i < countries.Length; i++)
        {
            var c = countries[i];
            if (c == null) continue;

            data.countries.Add(new CountryState
            {
                tag = c.gameObject.tag,
                ownerID = c.ownerID
            });
        }

        var units = mgr.GetAllUnits();
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;

            data.units.Add(new UnitState
            {
                ownerID = u.ownerID,
                unitType = (int)u.unitType,
                currentCountry = u.currentCountry,
                position = u.transform.position
            });
        }

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }

    public static SaveData LoadLatest()
    {
        if (!File.Exists(FilePath)) return null;
        var json = File.ReadAllText(FilePath);
        return JsonUtility.FromJson<SaveData>(json);
    }
}
