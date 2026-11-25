# FiresMonsterOverhaul

Monsters that are dangerous because of what they do, not just how hard they hit - and every new trick has a
counter you can learn.

## What's new in 0.0.51

- **Monster fall damage and monster armour fit settings moved here.** These used to be in FiresUnifiedCore's config
  and now live in this mod's own config. Your saved values carry over.
  - **Monster fall damage** (`[Movement]`, off by default) lets monsters, tames and NPCs take the same fall damage
    players do. It is a server setting: the server's value applies to everyone.
  - The **monster armour fit** settings (`[MonsterArmorFit]`) control how player armour looks when a monster wears
    it, such as how helmets sit on a monster's head. These are your own visual settings.
- **Creature Diving settings show properly.** The three Creature Diving settings now appear correctly in the settings
  menu, and the warning about them at start-up is gone.

## What you get

- **Readable casts.** A cast bar on the monster's nameplate shows what it is winding up. Stagger it (a parry or a
  heavy hit) or stun it during the wind-up to cancel the cast.
- **Smarter targeting.** Monsters remember who is hurting them and only switch targets when someone clearly draws
  more attention. Healing a player a monster is fighting draws its attention too.
- **Tiers.** Monsters can be Veteran, Elite or Champion, with extra abilities. Tiers get more common the farther you
  are from the world centre, and higher star levels mean higher tiers. Bosses and tamed creatures never get a tier.
- **Veteran abilities.** Casters get a Ground Blast, Brutes a Charge, Skirmishers a hit-and-run Sidestep. Red rings
  on the ground show where a hit will land.
- **Affixes.** Elites and Champions get special traits like Mending Aura, Stoneskin, Enrage, Vampiric, Blink,
  Summoner or Elemental - each one has a counter.
- **Champions** pause at half health to rally and gain one more trait.
- **Packs.** Some Meadows and Black Forest spawns arrive as a leader with followers.
- **Bosses.** Eikthyr gains Thunderhead, a charge lane and spirit stags; the Elder gains Entangle and a healing
  Heartwood phase. Moder, Yagluth, the Queen and Fader call in help and use new attacks at two-thirds and one-third
  health. Long fights trigger a soft enrage, and healing phases can be interrupted.
- **Creature diving.** With Fires Advanced Player Movement installed, creatures dive after a target in the water.
- **Set drops** (needs Fires Loot Overhaul): monsters can drop gear-set pieces as personal loot.

## How to use

Just play - the changes happen in the world on their own. Settings are in the Fires config window (F1). Only the main
on/off switches show there; turn on **Advanced** to see the rest. Every trait, boss phase and pack type can be turned
off on its own.

Server owners can also tune monster health, damage and star chances by biome, creature, dungeon, night or boss
progress in `BepInEx/config/FiresMonsterOverhaul.creatures.txt`. The file explains itself; nothing in it is active
until you uncomment a line.

## Install

- Install on every player **and** on the dedicated server. Monsters are run by whichever player is nearest them, so
  everyone needs the mod.
- The server's settings apply to every player.

## Requires

- BepInExPack for Valheim
- FiresUnifiedCore

## Good to know

- This mod is in beta - numbers may change as it is tuned.
- The new fighting style currently covers Greydwarfs and Draugr; tiers, traits and boss changes apply more widely.
- Creature tuning never changes bosses or tamed creatures, and never gives a monster fewer stars than vanilla did.
- Monster fall damage, creature diving and the monster armour fit settings are all in this mod's config.

## Issues / questions

https://discord.gg/zQRgHbqms4

## Screenshots

<!-- ![fmo_pack](HOSTED_URL/fmo_pack.png) -->
<!-- ![fmo_boss](HOSTED_URL/fmo_boss.png) -->
