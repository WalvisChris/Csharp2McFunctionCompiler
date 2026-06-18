# Example C# code
```cs
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
```

# Compiled mcfunction code
### start.mcfunction:
```mcfunction
scoreboard objectives add variables dummy
scoreboard objectives setdisplay sidebar variables
scoreboard players set $timer variables 0
say "Hello from C#!"
summon armor_stand ~ ~1 ~2
summon armor_stand 3 4 5 {Tags:["test","abc"],NoGravity:1b,Invisible:0b}
time set 6000
```

### update.mcfunction:
```mcfunction
execute if score $timer variables matches 11.. run say "10!"
execute if score $timer variables matches 11.. run scoreboard players set $timer variables 0
scoreboard players add $timer variables 1
```