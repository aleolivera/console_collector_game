using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UTN_TestGame;

namespace UTN_Parcial2 {
    internal class SaveSystem : ISaveSystem {
        private JsonSerializerOptions _options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        public string SaveDirectory = "Data";
        public string SaveFile = "Save.json";

        public SaveData SaveData { get; set ; }
        
        public SaveSystem() {
            SaveData = new SaveData();
        }
       
        public void DeleteSave() {
            if(!Directory.Exists(SaveDirectory))
                return;
           
            try {
                string filePath = Path.Combine(SaveDirectory, SaveFile);
                if(!File.Exists(filePath))
                    return;

                File.Delete(filePath);
            }
            catch(Exception) {
                Console.WriteLine("Unable to save game state.");
            }
        }

        public ISaveData Load() {
            try {
                string FilePath = Path.Combine(SaveDirectory, SaveFile);
                if(!File.Exists(FilePath))
                    return null;
                
                string jsonString = File.ReadAllText(FilePath);
                
                if(jsonString == null)
                    return null;

                SaveData.GameStateData = JsonSerializer.Deserialize<GameStateData>(jsonString);
            }
            catch(Exception) {
                Console.WriteLine("Unable to load game state.");
            }

            return SaveData; ;
        }

        public void Save() {
            try {
                if(!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);
                
                string filePath = Path.Combine(SaveDirectory, SaveFile);
                string jsonSave = JsonSerializer.Serialize(SaveData.GameStateData, _options);
                File.WriteAllText(filePath, jsonSave);
            }
            catch(Exception) {
                Console.WriteLine("Unable to save game state.");
            }
        }
    }
}
