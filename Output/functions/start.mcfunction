scoreboard objectives add variables dummy
scoreboard objectives setdisplay sidebar variables
scoreboard players set $timer variables 0
say "Hello from C#!"
summon armor_stand ~ ~1 ~2
summon armor_stand 3 4 5 {Tags:["test","abc"],NoGravity:1b,Invisible:0b}
time set 6000
