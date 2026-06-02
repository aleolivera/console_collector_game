using System;
using System.Collections.Generic;
using System.Text;
using UTN_TestGame;

namespace UTN_Parcial2 {
    internal class Application {
        static SaveSystem saveSystem = new SaveSystem();

        public static void Run() {
            Game game = new Game(saveSystem, typeof(Chest));

            game.OnTurnEnd += Game_OnTurnEnd;
            game.LoadData(saveSystem.SaveDirectory);
            game.Run();

            Console.Clear();
            Console.WriteLine("Thank you for playing!");
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private static void Game_OnTurnEnd(GameState obj) {
            saveSystem.SaveData.GameStateData.PlayerState = obj.playerState;
            saveSystem.SaveData.GameStateData.ChestsState = obj.chestsState;
        }
    }
}
