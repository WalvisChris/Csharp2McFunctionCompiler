namespace Minecraft.Library
{
    public enum Entity
    {
        Zombie,
        Armor_Stand
    }

    public struct Position
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public bool IsRelative { get; set; }

        public Position(float x, float y, float z, bool isRelative = false)
        {
            X = x;
            Y = y;
            Z = z;
            IsRelative = isRelative;
        }

        public static Position Relative(float x = 0, float y = 0, float z = 0) => new Position(x, y, z, isRelative: true);

        public static Position Absolute(float x, float y, float z) => new Position(x, y, z, isRelative: false);
    }

    public class Nbt
    {
        public bool NoAI { get; set; }
        public bool NoGravity { get; set; }
        public bool Invisible { get; set; }
        public bool Small { get; set; }
        public string[] Tags { get; set; }
        public string CustomName { get; set; }

        public static Nbt Create(System.Action<Nbt> initializer)
        {
            var nbt = new Nbt();
            initializer(nbt);
            return nbt;
        }
    }

    public enum ScoreboardDisplayMode
    {
        Sidebar,
        List,
        BelowName
    }

    public static class Command
    {
        public static void Say(string text) { }
        public static void Print(string message) { }
        public static void Summon(Entity entity, Position position, object nbt = null) { }
        public static void Raw(string minecraftCommand) { }
        public static void ScoreboardDisplay(ScoreboardDisplayMode mode) { }
    }
}