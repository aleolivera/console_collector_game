using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace UTN_TestGame {
    public interface ISaveData {
        public Vector2 GetPlayerPosition();
        public Direction GetPlayerRotation();
        public List<ChestState> GetActiveChests();
        public bool PlayerHasTreasure();
        public int GetPlayerCollectableCount();
    }

    public interface ISaveSystem {
        public void Save();
        public ISaveData Load();
        public void DeleteSave();
    }

    public struct ChestState {
        public bool IsEnabled { get; set; }
        public Vector2 Position { get; set; }
    }

    public struct PlayerState {
        public Vector2 Position { get; set; }
        public Direction Rotation { get; set; }
        public bool HasTreasure { get; set; }
        public int CollectableCount { get; set; }
    }

    public struct GameState {
        public PlayerState playerState;
        public List<ChestState> chestsState;
    }

    public struct Vector2 {
        public readonly static Vector2 Zero = new Vector2();
        public readonly static Vector2 One = new Vector2(1, 1);
        public readonly static Vector2 Left = new Vector2(-1, 0);
        public readonly static Vector2 Right = new Vector2(1, 0);
        public readonly static Vector2 Up = new Vector2(0, 1);
        public readonly static Vector2 Down = new Vector2(0, -1);

        public int X { get; set; }
        public int Y { get; set; }

        public Vector2(int x = 0, int y = 0) {
            X = x;
            Y = y;
        }

        public static Vector2 operator +(Vector2 left, Vector2 right) => new Vector2(left.X + right.X, left.Y + right.Y);
        public static Vector2 operator -(Vector2 left, Vector2 right) => new Vector2(left.X - right.X, left.Y - right.Y);

        public static Vector2 operator *(Vector2 left, int right) => new Vector2(left.X * right, left.Y * right);
        public static Vector2 operator *(int left, Vector2 right) => new Vector2(right.X * left, right.Y * left);

        public static bool operator ==(Vector2 left, Vector2 right) => right.X == left.X && right.Y == left.Y;
        public static bool operator !=(Vector2 left, Vector2 right) => right.X != left.X || right.Y != left.Y;

        public readonly override bool Equals([NotNullWhen(true)] object obj) {
            if(obj is not Vector2 vec2) return false;
            return X == vec2.X && Y == vec2.Y;
        }

        public readonly override int GetHashCode() => base.GetHashCode();

        public readonly override string ToString() => string.Format("({0}, {1})", X, Y);
    }

    public struct EntityMap {
        public char Wall { get; set; }
        public char Player { get; set; }
        public char Chest { get; set; }
        public char Treasure { get; set; }
    }

    public struct PlayerTurn {
        public MovementOptions movOptions;
        public List<string> actionOptions;
        public bool canInteract;

        public PlayerTurn() {
            movOptions = MovementOptions.All;
            actionOptions = new List<string>();
            canInteract = false;
        }
    }

    public abstract class GameEntity {
        public char MapChar { get; private set; }

        public Vector2 Position { get; protected set; }
        public Vector2 LastPosition { get; protected set; }
        public Direction Rotation { get; protected set; }

        bool _enabled = true;
        public bool Enabled {
            get => _enabled;

            set {
                _enabled = value;
                OnEntityToggled?.Invoke(this);
            }
        }

        public event Action<GameEntity> OnEntityToggled;

        public void SetMapChar(char mapChar) => MapChar = mapChar;

        public void TeleportEntity(Vector2 newPos) {
            LastPosition = Position;
            Position = newPos;
        }

        public void MoveEntity(MovementOptions dir) {
            Direction movementDirection = ClampRotation(Rotation + GetLocalRotationFromMovement(dir));
            Vector2 vecDir = GetDirectionVector(movementDirection);

            LastPosition = Position;
            Position += vecDir;
        }

        public void RotateEntity(Direction newDir) => Rotation = newDir;

        public void RotateEntity(MovementOptions dir) {
            int newRotation = GetLocalRotationFromMovement(dir);
            Rotation = ClampRotation(Rotation + newRotation);
        }

        protected static Direction GetDirection(MapLookUpDirection dir) => dir switch {
            MapLookUpDirection.UpLeft or MapLookUpDirection.Up or MapLookUpDirection.UpRight => Direction.Up,
            MapLookUpDirection.Right => Direction.Right,
            MapLookUpDirection.Left => Direction.Left,
            _ => Direction.Down,
        };

        public static Vector2 GetDirectionVector(Direction dir) => dir switch {
            Direction.Right => Vector2.Right,
            Direction.Down => Vector2.Up,
            Direction.Left => Vector2.Left,
            _ => Vector2.Down,
        };

        protected static int GetLocalRotationFromMovement(MovementOptions options) => options switch {
            MovementOptions.Forward => 0,
            MovementOptions.Right => 90,
            MovementOptions.Back => 180,
            _ => 270,
        };

        public static bool DirectionAvaible(MovementOptions options, MovementOptions mov) => (options & mov) == mov;

        protected static Direction ClampRotation(int rotation) => (Direction)((rotation + 360) % 360);
        protected static Direction ClampRotation(Direction rotation) => (Direction)(((int)rotation + 360) % 360);
    }

    public sealed class Player : GameEntity {
        public const int PLAYER_PVS_AREA = 9;
        public const int PLAYER_PVS_AREA_WIDTH = 3;

        public int PlayerPVS { get; private set; }
        public bool HasTreasure { get; set; }
        public int SpecialCollectable { get; set; }
        
        public char[] mapPortion;
        readonly Dictionary<char, Func<MapLookUpDirection, PlayerTurn, PlayerTurn>> actionMap;

        public Player(int pvs) {
            PlayerPVS = pvs;
            mapPortion = new char[PlayerPVS * PLAYER_PVS_AREA];

            actionMap = new Dictionary<char, Func<MapLookUpDirection, PlayerTurn, PlayerTurn>>();
            Rotation = Direction.Right;
        }

        public void SetupActionMap(EntityMap entityMap) {
            actionMap.Add(entityMap.Wall, OnWallAhead);
            actionMap.Add(entityMap.Chest, OnChestAhead);
            actionMap.Add(entityMap.Treasure, OnTreasureAhead);
        }

        public PlayerTurn ProcessActions() {
            PlayerTurn turn = new PlayerTurn();

            for(int i = 0; i < mapPortion.Length; i++) {
                if(!actionMap.TryGetValue(mapPortion[i], out var action)) continue;
                if(action == null) continue;
                turn = action((MapLookUpDirection)i, turn);
            }

            return turn;
        }

        public PlayerTurn OnWallAhead(MapLookUpDirection dir, PlayerTurn turn) {
            if(!ValidWallLookUpDirections(dir)) return turn;

            Direction transformedDir = ClampRotation(Rotation - GetDirection(dir));
            if(transformedDir < 0) transformedDir = 360 - transformedDir;

            switch(transformedDir) {
                case Direction.Up:
                    if(DirectionAvaible(turn.movOptions, MovementOptions.Right))
                        turn.movOptions ^= MovementOptions.Right;
                    break;

                case Direction.Down:
                    if(DirectionAvaible(turn.movOptions, MovementOptions.Left))
                        turn.movOptions ^= MovementOptions.Left;
                    break;

                case Direction.Left:
                    if(DirectionAvaible(turn.movOptions, MovementOptions.Back))
                        turn.movOptions ^= MovementOptions.Back;
                    break;

                case Direction.Right:
                    if(DirectionAvaible(turn.movOptions, MovementOptions.Forward))
                        turn.movOptions ^= MovementOptions.Forward;
                    break;
            }

            return turn;
        }

        public PlayerTurn OnChestAhead(MapLookUpDirection dir, PlayerTurn turn) {
            if(Rotation == GetDirection(dir)) {
                turn.actionOptions.Add(Locale.GetLocaleString("GAME_CHEST_AHEAD"));
                turn.canInteract = true;
            }
            else turn.actionOptions.Add(Locale.GetLocaleString("GAME_OBJECT_NEARBY").Replace("{ObjectName}", Locale.GetLocaleString("GAME_CHEST")));

            return OnWallAhead(dir, turn);
        }

        public PlayerTurn OnTreasureAhead(MapLookUpDirection dir, PlayerTurn turn) {
            if(Rotation == GetDirection(dir)) {
                turn.actionOptions.Add(Locale.GetLocaleString("GAME_TREASURE_AHEAD"));
                turn.canInteract = true;
            }
            else turn.actionOptions.Add(Locale.GetLocaleString("GAME_OBJECT_NEARBY").Replace("{ObjectName}", Locale.GetLocaleString("GAME_TREASURE")));

            return OnWallAhead(dir, turn);
        }

        public static bool ValidWallLookUpDirections(MapLookUpDirection dir) => dir switch {
            MapLookUpDirection.Up => true,
            MapLookUpDirection.Right => true,
            MapLookUpDirection.Left => true,
            MapLookUpDirection.Down => true,
            _ => false
        };

        public PlayerState GetCurrentState() => new PlayerState { Position = Position, Rotation = Rotation, HasTreasure = HasTreasure, CollectableCount = SpecialCollectable };

        public char GetObjectInFront() {
            Vector2 pos = GetDirectionVector(Rotation) + new Vector2(PLAYER_PVS_AREA_WIDTH / 2, PLAYER_PVS_AREA_WIDTH / 2);
            return mapPortion[pos.X + (pos.Y * PLAYER_PVS_AREA_WIDTH)];
        }
    }

    public abstract class InteractableObject : GameEntity {
        public abstract int OnInteract();
    }

    sealed class Treasure : InteractableObject {
        public override int OnInteract() {
            Enabled = false;
            return 1;
        }
    }

    sealed class GameMap {
        public const char EMPTY = '\0';

        public char WallChar { get; private set; } = EMPTY;

        readonly char[] map;
        readonly int height, width;

        public GameMap(int w, int h, char[] initialMap) {
            map = initialMap;
            height = h;
            width = w;
        }

        public Vector2 FindEntityPosition(char entityMapChar) {
            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    if(map[GetMapPos(x, y)] != entityMapChar) continue;
                    return new Vector2(x, y);
                }
            }

            return Vector2.One * -1;
        }

        public bool GrabMapPortion(Vector2 origin, int size, ref char[] mapPortion) {
            int mapPortionLength = mapPortion.Length;
            if(mapPortionLength < (size * Player.PLAYER_PVS_AREA)) return false;

            int totalHeight = (origin.Y + size) - (origin.Y - size) + 1;
            int totalWidth = (origin.X + size) - (origin.X - size) + 1;

            int pointer = 0;
            origin.X -= 1;
            origin.Y -= 1;

            for(int y = 0; y < totalHeight; y++) {
                for(int x = 0; x < totalWidth; x++) {
                    Vector2 posPointer = new Vector2(origin.X + x, origin.Y + y);

                    if(IsPositionOutOfBounds(posPointer)) {
                        mapPortion[pointer++] = WallChar;
                        continue;
                    }

                    mapPortion[pointer++] = map[GetMapPos(posPointer)];
                }
            }

            return true;
        }

        public bool IsPositionOutOfBounds(Vector2 pos) => pos.X < 0 || pos.X >= width || pos.Y < 0 || pos.Y >= height;

        int GetMapPos(Vector2 pos) => pos.X + (pos.Y * width);
        int GetMapPos(int x, int y) => x + (y * width);

        public void SetWallChar(char newChar) {
            if(WallChar != '\0') return;
            WallChar = newChar;
        }

        public bool UpdatePosition(Vector2 pos, char newObject) {
            if(map[GetMapPos(pos)] == WallChar) return false;
            map[GetMapPos(pos)] = newObject;
            return true;
        }

        public void DrawMap(StringBuilder strBuilder) {
            strBuilder.AppendFormat("{0}{0}", Environment.NewLine);

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int pos = GetMapPos(x, y);
                    strBuilder.AppendFormat("[{0}] ", map[pos] == EMPTY ? " " : map[pos]);
                }

                strBuilder.Append(Environment.NewLine);
            }
        }

        public void OnEntityToggled(GameEntity entity) {
            int pos = GetMapPos(entity.Position);
            if(entity.Enabled) {
                if(map[pos] != EMPTY) return;
                map[pos] = entity.MapChar;
                return;
            }

            map[pos] = EMPTY;
        }

        public Vector2 GetTreasurePosition(char treasureChar) {
            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    if(map[GetMapPos(x, y)] != treasureChar) continue;
                    return new Vector2(x, y);
                }
            }

            return Vector2.Zero;
        }

        public List<Vector2> GetAllChestsPosition(char chestChar) {
            List<Vector2> chestList = new List<Vector2>();

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    if(map[GetMapPos(x, y)] != chestChar) continue;
                    chestList.Add(new Vector2(x, y));
                }
            }

            return chestList;
        }
    }

    static class Locale {
        const string UNKNOWN_KEY = "INVALID LOCALE KEY";

        static Dictionary<string, string> localeDictionary;

        public static void LoadDictionary(string jsonString) => localeDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);

        public static string GetLocaleString(string key) {
            if(!localeDictionary.TryGetValue(key, out string value)) return UNKNOWN_KEY;
            return value;
        }
    }

    public enum MapLookUpDirection { UpLeft, Up, UpRight, Left, Middle, Right, DownLeft, Down, DownRight }
    public enum Direction { Up = 270, Right = 0, Down = 90, Left = 180 }

    [Flags]
    public enum MovementOptions {
        Trapped = 0,
        All = Left | Right | Forward | Back,
        Left = 0b1,
        Right = 0b10,
        Forward = 0b100,
        Back = 0b1000
    }

    public enum CommandAction { Walk, Rotate, Wait, Interact }

    public sealed class Game {
        readonly GameMap gameMap;
        readonly Player player;

        MovementOptions avaibleMovementOptions;
        readonly MovementOptions validRotations;

        Vector2 exitPos;
        EntityMap entityMap;

        readonly Dictionary<CommandAction, Action<string>> actionMap;
        readonly Dictionary<string, Action<string>> commandActions;

        readonly StringBuilder strBuilder;
        readonly List<GameEntity> entities;

        readonly Type implementedChestType;
        readonly ISaveSystem saveSystem;

        public event Action<GameState> OnTurnEnd;

        public Game(ISaveSystem saveSys, Type chestType) {
            gameMap = new GameMap(7, 9, new char[]
            {
                'W', 'W', 'W', 'W', 'W', 'W', 'T',
                'P', '\0', '\0', 'W', '\0', 'W', '\0',
                'W', 'W', '\0', '\0', '\0', '\0', '\0',
                'W', 'W', 'W', '\0', '\0', '\0', '\0',
                '\0', 'C', 'W', '\0', 'W', '\0', 'W',
                '\0', '\0', 'W', '\0', 'W', '\0', 'W',
                '\0', '\0', '\0', '\0', 'W', '\0', 'W',
                'C', '\0', 'W', '\0', 'W', 'C', 'W',
                '\0', '\0', 'W', 'W', 'W', 'W', 'W',
            });

            strBuilder = new StringBuilder();

            player = new Player(1);
            entities = new List<GameEntity> { player };

            actionMap = new Dictionary<CommandAction, Action<string>>
            {
                { CommandAction.Walk, WalkAction },
                { CommandAction.Rotate, RotateAction },
                { CommandAction.Wait, WaitAction },
                { CommandAction.Interact, InteractAction }
            };

            commandActions = new Dictionary<string, Action<string>>();
            validRotations = MovementOptions.Right | MovementOptions.Left;

            saveSystem = saveSys;

            implementedChestType = chestType;
        }

        public bool LoadData(string dataPath) {
            try {
                if(!Directory.Exists(dataPath))
                    throw new Exception(string.Format("{0} directory doesn't exist!", dataPath));

                string json = File.ReadAllText(Path.Combine(dataPath, "EntityMap.json"));
                entityMap = JsonSerializer.Deserialize<EntityMap>(json);

                json = File.ReadAllText(Path.Combine(dataPath, "Locale.json"));
                Locale.LoadDictionary(json);

                json = File.ReadAllText(Path.Combine(dataPath, "Cmd.json"));
                Dictionary<string, string> comMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                foreach(KeyValuePair<string, string> valuePair in comMap) {
                    if(!Enum.TryParse(valuePair.Value, true, out CommandAction cmdAction)) continue;
                    if(!actionMap.TryGetValue(cmdAction, out Action<string> action)) continue;
                    commandActions.Add(valuePair.Key.ToLower(), action);
                }
            }
            catch(Exception e) {
                Console.Clear();
                Console.WriteLine(e.Message);
                return false;
            }

            SetupGame();

            if(saveSystem == null) return false;

            ISaveData data = saveSystem.Load();
            if(data == null) return true;

            List<ChestState> activeChests = data.GetActiveChests();
            for(int i = 0; i < activeChests.Count; i++) {
                for(int j = 0; j < entities.Count; j++) {
                    if(entities[j].MapChar != entityMap.Chest || entities[j].Position != activeChests[i].Position) continue;
                    entities[j].Enabled = activeChests[i].IsEnabled;
                    break;
                }
            }

            gameMap.UpdatePosition(player.Position, '\0');
            player.TeleportEntity(data.GetPlayerPosition());
            gameMap.UpdatePosition(player.Position, player.MapChar);

            player.RotateEntity(data.GetPlayerRotation());
            player.HasTreasure = data.PlayerHasTreasure();
            player.SpecialCollectable = data.GetPlayerCollectableCount();

            if(player.HasTreasure) {
                GameEntity treasure = FindFirstEntity(entityMap.Treasure);
                if(treasure != null) treasure.Enabled = false;
            }

            return true;
        }

        void SetupGame() {
            player.SetMapChar(entityMap.Player);

            exitPos = gameMap.FindEntityPosition(player.MapChar);
            player.TeleportEntity(exitPos);
            player.SetupActionMap(entityMap);

            gameMap.SetWallChar(entityMap.Wall);

            InteractableObject treasure = CreateInteractableObject(typeof(Treasure));
            treasure.SetMapChar(entityMap.Treasure);
            treasure.TeleportEntity(gameMap.GetTreasurePosition(treasure.MapChar));
            entities.Add(treasure);

            List<Vector2> chestsOnMap = gameMap.GetAllChestsPosition(entityMap.Chest);
            for(int i = 0; i < chestsOnMap.Count; i++) {
                InteractableObject chest = CreateInteractableObject(implementedChestType);
                chest.SetMapChar(entityMap.Chest);
                chest.TeleportEntity(chestsOnMap[i]);
                entities.Add(chest);
            }
        }

        public void Run() {
            try {
                while(true) {
                    Console.Clear();
                    strBuilder.Clear();

                    if(ProcessPlayerSituation())
                        break;

                    ProcessPlayerInput();
                    ProcessGameEntities();
                    Debug_DrawMap();

                    saveSystem?.Save();
                    Console.ReadKey();
                }
            }
            catch(Exception e) {
                Console.Clear();
                Console.WriteLine(e.Message);
            }
        }

        bool ProcessPlayerSituation() {
            if(CheckWinningCondition()) return true;

            if(!gameMap.GrabMapPortion(player.Position, player.PlayerPVS, ref player.mapPortion))
                throw new Exception(Locale.GetLocaleString("GAME_ERROR_PORTION"));

            PlayerTurn playerTurn = player.ProcessActions();
            avaibleMovementOptions = playerTurn.movOptions;

            if(playerTurn.movOptions != MovementOptions.All) {
                int avaibleCount = BitOperations.PopCount((uint)avaibleMovementOptions);

                strBuilder.Append(Locale.GetLocaleString(avaibleCount switch {
                    1 => "GAME_DEADEND",
                    2 => TwoMovementOptionsAvaible(avaibleMovementOptions),
                    3 => ThreeMovementOptionsAvaible(avaibleMovementOptions),
                    _ => "GAME_TRAPPED"
                }));
            }
            else
                strBuilder.Append(Locale.GetLocaleString("GAME_HALL"));

            int actionCount = playerTurn.actionOptions.Count;
            if(actionCount > 0) strBuilder.AppendFormat(". ");

            for(int i = 0; i < actionCount; i++) {
                strBuilder.Append(playerTurn.actionOptions[i]);
                if(actionCount - i > 1) strBuilder.Append(", ");
            }

            strBuilder.AppendFormat("\n{0} ", Locale.GetLocaleString("GAME_OPTIONS"));

            strBuilder.Append("[ ");
            if(GameEntity.DirectionAvaible(avaibleMovementOptions, MovementOptions.Forward)) strBuilder.AppendFormat("{0} ", Locale.GetLocaleString("GAME_FORWARD"));
            if(GameEntity.DirectionAvaible(avaibleMovementOptions, MovementOptions.Left)) strBuilder.AppendFormat("{0} ", Locale.GetLocaleString("GAME_LEFT"));
            if(GameEntity.DirectionAvaible(avaibleMovementOptions, MovementOptions.Right)) strBuilder.AppendFormat("{0} ", Locale.GetLocaleString("GAME_RIGHT"));
            if(GameEntity.DirectionAvaible(avaibleMovementOptions, MovementOptions.Back)) strBuilder.AppendFormat("{0} ", Locale.GetLocaleString("GAME_BACK"));
            if(playerTurn.canInteract) strBuilder.AppendFormat("{0} ", Locale.GetLocaleString("GAME_INTERACT"));
            strBuilder.AppendFormat("]\n{0}\n", Locale.GetLocaleString("GAME_CHOOSE"));

            Console.Write(strBuilder.ToString());
            return false;
        }

        static string TwoMovementOptionsAvaible(MovementOptions mov) => mov switch {
            MovementOptions.Left | MovementOptions.Right => "GAME_HALLWAY",
            MovementOptions.Forward | MovementOptions.Back => "GAME_HALLWAY",
            _ => "GAME_WALL_CORNER",
        };

        static string ThreeMovementOptionsAvaible(MovementOptions mov) => (MovementOptions.All & ~mov) switch {
            MovementOptions.Left => "GAME_WALL_LEFT",
            MovementOptions.Right => "GAME_WALL_RIGHT",
            MovementOptions.Forward => "GAME_WALL_FORWARD",
            _ => "GAME_WALL_BACK"
        };

        void ProcessPlayerInput() {
            strBuilder.Clear();

            string playerInput = Console.ReadLine();
            string[] commands = playerInput.ToLower().Split(' ');

            if(!commandActions.TryGetValue(commands[0].ToLower(), out Action<string> action))
                strBuilder.Append(Locale.GetLocaleString("GAME_UNKNOWN_COMMAND"));
            else
                action?.Invoke(commands.Length > 1 ? commands[1] : "");

            Console.Clear();
            Console.Write(strBuilder.ToString());
        }

        InteractableObject CreateInteractableObject(Type type) {
            if(!typeof(InteractableObject).IsAssignableFrom(type))
                throw new ArgumentException(Locale.GetLocaleString("GAME_ERROR_TYPE"));

            InteractableObject obj = Activator.CreateInstance(type) as InteractableObject;
            obj.OnEntityToggled += gameMap.OnEntityToggled;
            return obj;
        }

        void ProcessGameEntities() {
            List<ChestState> activeChests = new List<ChestState>();

            for(int i = 0; i < entities.Count; i++) {
                if(entities[i].MapChar == entityMap.Chest) {
                    activeChests.Add(new ChestState { Position = entities[i].Position, IsEnabled = entities[i].Enabled });
                    continue;
                }

                if(!entities[i].Enabled) continue;

                if(entities[i].LastPosition == entities[i].Position) continue;
                gameMap.UpdatePosition(entities[i].LastPosition, '\0');
                gameMap.UpdatePosition(entities[i].Position, entities[i].MapChar);
                entities[i].TeleportEntity(entities[i].Position);
            }
            OnTurnEnd?.Invoke(new GameState { playerState = player.GetCurrentState(), chestsState = activeChests });
        }

        void WalkAction(string command) {
            if(!Enum.TryParse(command, true, out MovementOptions newDirection)) {
                strBuilder.Append(Locale.GetLocaleString("GAME_UNKNOWN_COMMAND"));
                return;
            }

            if(!GameEntity.DirectionAvaible(avaibleMovementOptions, newDirection)) {
                strBuilder.Append(Locale.GetLocaleString($"GAME_WALK_INTOWALL"));
                return;
            }

            player.MoveEntity(newDirection);
            strBuilder.Append(Locale.GetLocaleString($"GAME_ACTION_MOVE_{command.ToUpper()}"));
        }

        void RotateAction(string command) {
            if(!Enum.TryParse(command, true, out MovementOptions newDirection)) {
                strBuilder.Append(Locale.GetLocaleString("GAME_UNKNOWN_COMMAND"));
                return;
            }

            if(!GameEntity.DirectionAvaible(validRotations, newDirection)) {
                strBuilder.Append(Locale.GetLocaleString("GAME_ROTATE_INVALID"));
                return;
            }

            player.RotateEntity(newDirection);
            strBuilder.Append(Locale.GetLocaleString($"GAME_ROTATE_{command.ToUpper()}"));
        }

        void InteractAction(string command) {
            char objectType = player.GetObjectInFront();
            GameEntity obj = FindEntity(objectType, player.Position + GameEntity.GetDirectionVector(player.Rotation));

            if(obj == null || obj is not InteractableObject interactable) {
                strBuilder.Append(Locale.GetLocaleString("GAME_INTERACTION_INVALID"));
                return;
            }

            int ammount = interactable.OnInteract();

            if(objectType == entityMap.Treasure) {
                player.HasTreasure = true;
                strBuilder.Append(Locale.GetLocaleString("GAME_INTERACTION_TREASURE"));
                return;
            }

            player.SpecialCollectable += ammount;
            strBuilder.Append(FormatCollectable(Locale.GetLocaleString("GAME_INTERACTION_CHEST"), ammount));
        }

        void WaitAction(string command) {
            strBuilder.Append(Locale.GetLocaleString("GAME_ACTION_WAIT"));
        }

        bool CheckWinningCondition() {
            if(!player.HasTreasure || player.Position != exitPos) return false;
            Console.Write(FormatCollectable(Locale.GetLocaleString("GAME_WIN"), player.SpecialCollectable));
            saveSystem.DeleteSave();
            return true;
        }

        static string FormatCollectable(string message, int collAmmount) => message.
            Replace("{Collectable_Count}", collAmmount.ToString()).
            Replace("{Collectable}", Locale.GetLocaleString("GAME_COLLECTABLES"));

        GameEntity FindEntity(char mapChar, Vector2 pos) {
            for(int i = 0; i < entities.Count; i++) {
                if(entities[i].MapChar != mapChar || entities[i].Position != pos) continue;
                return entities[i];
            }

            return null;
        }

        GameEntity FindFirstEntity(char mapChar) {
            for(int i = 0; i < entities.Count; i++) {
                if(entities[i].MapChar != mapChar) continue;
                return entities[i];
            }

            return null;
        }

        void Debug_DrawMap() {
#if !DEBUG_MAP
            strBuilder.Clear();
            gameMap.DrawMap(strBuilder);
            Console.Write(strBuilder.ToString());
#endif
        }
    }
}
