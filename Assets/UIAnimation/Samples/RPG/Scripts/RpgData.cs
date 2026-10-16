using System.Collections.Generic;
using UnityEngine;

/// <summary>The RPG sample's game data (heroes, items, quests, chapters): plain lists, no saving.</summary>
public static class RpgData
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    public sealed class Hero
    {
        public string id, name, role, weapon;
        public int stars, level, atk, def, hp, spd;
        public (string icon, string name, string text)[] skills;
        public string story;
    }

    public sealed class Item
    {
        public string icon, name, text;
        public int tab, count;
        public Rarity rarity;
    }

    public sealed class Quest
    {
        public string section, title, rewardIcon;
        public int progress, goal, reward;
        public bool Complete => progress >= goal;
    }

    public sealed class Chapter
    {
        public string name, icon;
        public int cleared;   // stages cleared (of 10)
    }

    public static readonly Hero[] Heroes =
    {
        Make("aria", "Aria", "Ranger", "bow", 5, 42, 980, 420, 5200, 138,
            ("skill_arrow", "Volley", "Fires a storm of arrows at every enemy."), ("skill_arcane", "Moonlight", "Heals allies a little each turn."),
            ("skill_ice", "Frost Shot", "Slows the target for two turns."), "A wandering elf who never misses twice."),
        Make("merlin", "Merlin", "Archmage", "staff", 5, 38, 1120, 300, 4300, 104,
            ("skill_fire", "Meteor", "Huge fire damage to one area."), ("skill_ice", "Blizzard", "Freezes the front row."),
            ("skill_arcane", "Arcane Ward", "A shield that absorbs magic."), "He remembers the first spell ever cast."),
        Make("borg", "Borg", "Berserker", "axe", 4, 35, 1060, 520, 7400, 112,
            ("skill_fire", "Rage", "Attack up as health goes down."), ("skill_arrow", "Cleave", "Hits the target and its neighbours."),
            ("skill_arcane", "Roar", "Taunts every enemy."), "Came down from the mountains for a fight."),
        Make("durin", "Durin", "Guardian", "shield", 4, 33, 610, 940, 8600, 82,
            ("skill_ice", "Stone Skin", "Takes half damage for a turn."), ("skill_fire", "Forge Hammer", "Stuns a heavy enemy."),
            ("skill_arcane", "Rally", "Defense up for all allies."), "His shield has never touched the ground."),
        Make("leon", "Leon", "Paladin", "sword", 3, 27, 720, 700, 6900, 96,
            ("skill_arcane", "Holy Light", "Heals the weakest ally."), ("skill_fire", "Judgement", "Bonus damage to dark enemies."),
            ("skill_ice", "Aegis", "Blocks the next hit on an ally."), "A young knight sworn to the old crown."),
        Make("vex", "Vex", "Assassin", "dagger", 3, 24, 1010, 260, 3900, 147,
            ("skill_arrow", "Backstab", "Critical hit from the shadows."), ("skill_arcane", "Vanish", "Can't be targeted for a turn."),
            ("skill_fire", "Poison Blade", "Damage over three turns."), "Nobody has seen Vex's face. Probably."),
    };

    public static readonly (string name, int max)[] Stats = { ("ATK", 1200), ("DEF", 1000), ("HP", 9000), ("SPD", 150) };
    public static readonly string[] StatIcons = { "atk", "def", "hp", "spd" };
    public static int Stat(Hero h, int i) => i switch { 0 => h.atk, 1 => h.def, 2 => h.hp, _ => h.spd };

    public static readonly string[] Tabs = { "EQUIPMENT", "POTIONS", "MATERIALS" };

    public static readonly Item[] Items =
    {
        I(0, "staff", "Archmage Staff", Rarity.Legendary, 1, "Spells cost no mana once per battle."),
        I(0, "ring", "Ring of Ages", Rarity.Legendary, 1, "The wearer revives once with half health."),
        I(0, "bow", "Elven Longbow", Rarity.Epic, 1, "Arrows pierce the first enemy."),
        I(0, "armor", "Dragon Plate", Rarity.Epic, 1, "Fire damage taken -50%."),
        I(0, "dagger", "Shadow Fang", Rarity.Epic, 1, "Critical hits poison the target."),
        I(0, "sword", "Knight's Blade", Rarity.Rare, 1, "Attack +120."),
        I(0, "shield", "Tower Shield", Rarity.Rare, 1, "Defense +150."),
        I(0, "necklace", "Emerald Charm", Rarity.Rare, 1, "Healing received +20%."),
        I(0, "axe", "Iron Axe", Rarity.Common, 2, "Attack +60."),
        I(0, "helm", "Iron Helm", Rarity.Common, 3, "Defense +40."),
        I(0, "boots", "Traveler Boots", Rarity.Common, 1, "Speed +8."),
        I(1, "book", "Skill Tome", Rarity.Epic, 2, "Teaches a hero a new skill."),
        I(1, "potion_green", "Elixir", Rarity.Rare, 5, "Restores health and mana."),
        I(1, "scroll", "Teleport Scroll", Rarity.Rare, 8, "Leaves a battle without losing."),
        I(1, "potion_red", "Health Potion", Rarity.Common, 24, "Restores 500 health."),
        I(1, "potion_blue", "Mana Potion", Rarity.Common, 12, "Restores 200 mana."),
        I(2, "seal", "Royal Seal", Rarity.Legendary, 1, "Proof of the king's favour."),
        I(2, "gems", "Soul Gems", Rarity.Epic, 6, "Used to awaken heroes."),
        I(2, "crystal", "Mana Crystal", Rarity.Rare, 40, "Used to upgrade staffs and rings."),
        I(2, "rune", "Rune Stone", Rarity.Common, 33, "Used to upgrade armor."),
        I(2, "gold_bar", "Gold Bar", Rarity.Common, 15, "Sell for 1,000 gold."),
    };

    public static readonly Chapter[] Chapters =
    {
        new() { name = "Whispering Woods", icon = "ch_forest", cleared = 10 },
        new() { name = "Frost Peaks", icon = "ch_mountain", cleared = 10 },
        new() { name = "Ember Caldera", icon = "ch_volcano", cleared = 6 },
        new() { name = "Shadow Keep", icon = "ch_castle", cleared = 0 },
        new() { name = "Dragon's Lair", icon = "ch_dragon", cleared = 0 },
    };

    public static List<Quest> Quests() => new()
    {
        Q("DAILY", "Win 3 battles", 3, 3, "gold", 500), Q("DAILY", "Summon a hero", 1, 1, "gem", 50),
        Q("DAILY", "Use 5 potions", 2, 5, "gold", 300), Q("DAILY", "Upgrade a hero", 0, 1, "stamina", 20),
        Q("WEEKLY", "Clear chapter 2", 1, 1, "gem", 300), Q("WEEKLY", "Win 30 battles", 18, 30, "gold", 3000),
        Q("WEEKLY", "Summon 10 heroes", 10, 10, "gem", 500), Q("WEEKLY", "Spend 5,000 gold", 3200, 5000, "gold", 1000),
        Q("ACHIEVEMENTS", "Collect 6 heroes", 6, 6, "gem", 1000), Q("ACHIEVEMENTS", "Reach level 30", 27, 30, "gold", 5000),
        Q("ACHIEVEMENTS", "Own a legendary item", 1, 1, "gem", 200), Q("ACHIEVEMENTS", "Defeat the dragon", 0, 1, "gem", 3000),
    };

    // ---------- 2nd set: shop, mail, attendance, loading ----------

    public sealed class Offer
    {
        public string icon, name, currency;   // currency: "gold" or "gem" (what it costs)
        public int price, stock;               // stock: how many times it can be bought (-1 = any)
        public int gold, gems, stamina;        // what it gives
    }

    public static List<Offer> Offers() => new()
    {
        O("chest", "Hero Chest", "gem", 900, 1, gems: 0, gold: 8000), O("gems", "Handful of Gems", "gold", 6000, 3, gems: 120),
        O("stamina", "Stamina Pack", "gem", 100, 5, stamina: 30), O("gold_bar", "Gold Bars", "gem", 200, -1, gold: 5000),
        O("potion_red", "Potion Bundle", "gold", 1500, 10, gold: 0, stamina: 6), O("crystal", "Mana Crystals", "gem", 300, 2, gold: 2500),
        O("rune", "Rune Pouch", "gold", 2400, 4, gold: 0, gems: 20), O("seal", "Royal Seal", "gem", 2000, 1, gold: 20000),
    };

    public sealed class Mail
    {
        public string from, title, icon;
        public int gold, gems, stamina;
    }

    public static List<Mail> Mails() => new()
    {
        M("Guild Master", "Welcome to the guild!", "gem", gems: 300), M("Royal Court", "Maintenance compensation", "gold", gold: 5000),
        M("Event Team", "Summon Festival bonus", "gem", gems: 150), M("Innkeeper", "A warm meal", "stamina", stamina: 30),
        M("Royal Court", "Daily login (Day 4)", "gold", gold: 2000), M("Merlin", "Found this in the library", "gem", gems: 80),
    };

    /// <summary>Attendance rewards, day 1–7 (icon, amount). The 7th is the big one.</summary>
    public static readonly (string icon, int amount)[] Attendance =
    {
        ("gold", 1000), ("stamina", 20), ("gem", 50), ("gold", 3000), ("gem", 100), ("stamina", 40), ("chest", 1),
    };

    public static readonly string[] Tips =
    {
        "Tip: Guardians hold the front row; put them first.",
        "Tip: Summon Festival doubles legendary rates this week.",
        "Tip: Clear every stage of a chapter to open the next one.",
    };

    // ---------- 3rd set: guild chat, ranking, story ----------

    public static readonly (string who, string hero, string text)[] GuildChat =
    {
        ("Merlin", "merlin", "Raid starts at nine. Bring potions!"),
        ("Borg", "borg", "I was born ready."),
        ("Vex", "vex", "Someone saw the dragon near the Frost Peaks..."),
        ("Durin", "durin", "Guardians up front, please. Last time was a mess."),
    };

    /// <summary>Who answers a typed message, and how (in turn).</summary>
    public static readonly (string who, string hero, string answer)[] ChatReplies =
    {
        ("Merlin", "merlin", "Good. The portal closes in ten minutes."), ("Aria", "aria", "Ha! Good one."), ("Durin", "durin", "Noted, Commander."),
        ("Leon", "leon", "For the crown!"),
    };

    public sealed class Rank
    {
        public string name, hero;
        public int score;
        public bool me;
    }

    /// <summary>The weekly ranking, best first; the player is somewhere in the middle.</summary>
    public static List<Rank> Ranking()
    {
        string[] names = { "Shadowfang", "Lumina", "IronWill", "Nightshade", "Ember", "Frostbite", "Valkyr", "Thorn", "Seraph", "Grimm" };
        string[] heroes = { "vex", "aria", "durin", "merlin", "borg", "leon" };
        var list = new List<Rank>();
        for (var i = 0; i < 50; i++)
            list.Add(new Rank { name = i == 23 ? "COMMANDER" : names[i % names.Length] + (i >= names.Length ? (i / names.Length).ToString() : ""), hero = heroes[i % heroes.Length], score = 98000 - i * 1730 - i * i * 7, me = i == 23 });
        return list;
    }

    public static readonly (string who, string hero, bool right, string text)[] Story =
    {
        ("Aria", "aria", false, "The forest is too quiet tonight, Commander. Even the owls have fled."),
        ("Merlin", "merlin", true, "I feel it too. Something old is waking beneath the Ember Caldera."),
        ("Aria", "aria", false, "Then we march at dawn. Will you lead us?"),
    };

    public static readonly string[] StoryChoices = { "We march at dawn", "Wait for the guild" };

    static Offer O(string icon, string name, string currency, int price, int stock, int gold = 0, int gems = 0, int stamina = 0) =>
        new() { icon = icon, name = name, currency = currency, price = price, stock = stock, gold = gold, gems = gems, stamina = stamina };

    static Mail M(string from, string title, string icon, int gold = 0, int gems = 0, int stamina = 0) =>
        new() { from = from, title = title, icon = icon, gold = gold, gems = gems, stamina = stamina };

    public static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Legendary => new Color32(255, 196, 70, 255),
        Rarity.Epic => new Color32(190, 110, 255, 255),
        Rarity.Rare => new Color32(90, 160, 255, 255),
        _ => new Color32(170, 170, 170, 255),
    };

    public static Color StarColor(int stars) => RarityColor(stars >= 5 ? Rarity.Legendary : stars == 4 ? Rarity.Epic : Rarity.Rare);

    static Hero Make(string id, string name, string role, string weapon, int stars, int level, int atk, int def, int hp, int spd,
        (string, string, string) s1, (string, string, string) s2, (string, string, string) s3, string story) =>
        new() { id = id, name = name, role = role, weapon = weapon, stars = stars, level = level, atk = atk, def = def, hp = hp, spd = spd, skills = new[] { s1, s2, s3 }, story = story };

    static Item I(int tab, string icon, string name, Rarity rarity, int count, string text) => new() { tab = tab, icon = icon, name = name, rarity = rarity, count = count, text = text };

    static Quest Q(string section, string title, int progress, int goal, string icon, int reward) =>
        new() { section = section, title = title, progress = progress, goal = goal, rewardIcon = icon, reward = reward };
}
