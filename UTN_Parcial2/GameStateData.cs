using System;
using System.Collections.Generic;
using System.Text;
using UTN_TestGame;

namespace UTN_Parcial2 {
    class GameStateData {
        public PlayerState PlayerState { get; set; }
        public List<ChestState> ChestsState { get; set; } = new List<ChestState>();

    }
}
