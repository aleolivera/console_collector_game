using System;
using System.Collections.Generic;
using System.Text;
using UTN_TestGame;

namespace UTN_Parcial2 {
    internal class SaveData : ISaveData {
        public GameState _gameState;

        public GameStateData GameStateData {
            get;
            set {
                _gameState.playerState = value.PlayerState;
                _gameState.chestsState = value.ChestsState;
            }
        } = new GameStateData();
        
        public SaveData() {
            _gameState = new GameState();
            _gameState.chestsState = new List<ChestState>();
        }

        public List<ChestState> GetActiveChests() {
            return _gameState.chestsState;
        }

        public int GetPlayerCollectableCount() {
            return _gameState.playerState.CollectableCount;
        }

        public Vector2 GetPlayerPosition() {
            return _gameState.playerState.Position;
        }

        public Direction GetPlayerRotation() {
            return _gameState.playerState.Rotation;
        }

        public bool PlayerHasTreasure() {
            return _gameState.playerState.HasTreasure;
        }
    }
}
