// ============================================================================
// JSON Material Repository — Persistent storage for materials.
//
// Implements IMaterialRepository.
// Saves and loads materials from a JSON file in persistent data path.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Persistence
{
    /// <summary>
    /// File-based repository for materials using JSON serialization.
    /// </summary>
    public sealed class JsonMaterialRepository : IMaterialRepository
    {
        private readonly string _filePath;

        [Serializable]
        private class MaterialDataWrapper
        {
            public List<MaterialEntry> materials = new List<MaterialEntry>();
        }

        [Serializable]
        private class MaterialEntry
        {
            public string name;
            public double absorption;
            public double thermal_conductivity;
            public double density;
            public double specific_heat;
            public double t_ambient;
            public double t_melt;
            public double t_vaporization;
        }

        public JsonMaterialRepository(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _filePath = Path.Combine(Application.persistentDataPath, "materials.json");
            }
            else
            {
                _filePath = filePath;
            }
            
            EnsureFileExists();
        }

        private void EnsureFileExists()
        {
            if (!File.Exists(_filePath))
            {
                var defaultWrapper = new MaterialDataWrapper();
                // Add default WC-NiCr material
                defaultWrapper.materials.Add(new MaterialEntry
                {
                    name = "WC-NiCr",
                    absorption = 0.75,
                    thermal_conductivity = 45.0,
                    density = 14800,
                    specific_heat = 300,
                    t_ambient = 20.0,
                    t_melt = 1400.0,
                    t_vaporization = 2800.0
                });
                string json = JsonUtility.ToJson(defaultWrapper, true);
                File.WriteAllText(_filePath, json);
            }
        }

        public void Save(Dictionary<string, object> materialData)
        {
            var wrapper = LoadWrapper();
            
            string name = materialData.ContainsKey("name") ? materialData["name"].ToString() : "Unknown";
            
            // Remove existing with same name
            wrapper.materials.RemoveAll(m => m.name == name);
            
            // Map dictionary to strictly typed object for JsonUtility
            var newEntry = new MaterialEntry
            {
                name = name,
                absorption = GetDouble(materialData, "absorption", 0.5),
                thermal_conductivity = GetDouble(materialData, "thermal_conductivity", 45.0),
                density = GetDouble(materialData, "density", 8000),
                specific_heat = GetDouble(materialData, "specific_heat", 400),
                t_ambient = GetDouble(materialData, "t_ambient", 20.0),
                t_melt = GetDouble(materialData, "t_melt", 1500.0),
                t_vaporization = GetDouble(materialData, "t_vaporization", 2800.0)
            };
            
            wrapper.materials.Add(newEntry);
            
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(_filePath, json);
        }

        public List<Dictionary<string, object>> LoadAll()
        {
            var wrapper = LoadWrapper();
            var result = new List<Dictionary<string, object>>();
            
            foreach (var entry in wrapper.materials)
            {
                result.Add(new Dictionary<string, object>
                {
                    { "name", entry.name },
                    { "absorption", entry.absorption },
                    { "thermal_conductivity", entry.thermal_conductivity },
                    { "density", entry.density },
                    { "specific_heat", entry.specific_heat },
                    { "t_ambient", entry.t_ambient },
                    { "t_melt", entry.t_melt },
                    { "t_vaporization", entry.t_vaporization }
                });
            }
            
            return result;
        }

        public List<Dictionary<string, object>> FindBest(int n = 5)
        {
            var all = LoadAll();
            // Just return the first n for now, as "best" requires specific sorting criteria
            if (all.Count <= n) return all;
            return all.GetRange(0, n);
        }
        
        private MaterialDataWrapper LoadWrapper()
        {
            if (!File.Exists(_filePath)) return new MaterialDataWrapper();
            
            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonUtility.FromJson<MaterialDataWrapper>(json) ?? new MaterialDataWrapper();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonMaterialRepository] Error loading materials: {ex.Message}");
                return new MaterialDataWrapper();
            }
        }
        
        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out object val))
            {
                if (val is IConvertible convertible)
                {
                    return convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            return defaultValue;
        }
    }
}
