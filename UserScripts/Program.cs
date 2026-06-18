using Minecraft.Library;
using System.Numerics;
using System.Timers;

public class DataPack
{
    public static int timer;

    void Start()
    {
        Command.ScoreboardDisplay(ScoreboardDisplayMode.Sidebar);
        
        timer = 0;
        Command.Say("Hello from C#!");
        Command.Summon(Entity.Armor_Stand, Position.Relative(0f, 1f, 2f));
        Command.Summon(Entity.Armor_Stand, Position.Absolute(3f, 4f, 5f), new Nbt
        {
            Tags = ["test","abc"],
            NoGravity = true,
            Invisible = false
        });

        Command.Raw("time set 6000");
    }

    void Update()
    {
        if (timer > 10)
        {
            Command.Say("10!");
            timer = 0;
        }
        
        timer += 1;
    }
}