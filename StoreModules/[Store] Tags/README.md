# [Store] Tags Plugin

A powerful and highly customizable tag system for Counter-Strike 2 servers, designed to integrate seamlessly with a Store API and provide permission-based tags for administrators, VIPs, and more.

## Overview

This plugin allows players to display custom tags in chat and on the scoreboard. It features a unique **multi-tag system** where players can equip up to two tags simultaneously, combining tags they've purchased from the store with tags assigned to them via permissions.

## Features

- **Multi-Tag System:** Equip up to two tags at once (e.g., a store tag + a permission tag).
- **Store Integration:** Register tags as items for players to purchase through the Store API.
- **Permission-Based Static Tags:** Assign tags to players based on their SteamID, admin flags (`@css/flag`), or admin groups (`#group`).
- **Full Chat Customization:** Control the color of the tag, player name, and chat message.
- **Scoreboard Tags:** Display a custom clan tag on the scoreboard.
- **Interactive Menu:** A user-friendly menu (`!tags`) for players to manage and toggle their available tags.
- **Dual Configuration System:** Separates core plugin settings from the items sold in the store for clean management.

## Dependencies

- **[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)**

> **Important:** The **Custom Tag** feature requires the `T3MenuSharedApi` because it uses text input fields to allow players to create their tag. If you do not use this menu API, the custom tag item will not function correctly.

## Configuration: The Two-File System

This plugin uses two separate configuration files for different purposes. Understanding the role of each is essential.

### 1. Main Plugin Configuration (`[Store] Tags.toml`)

This is the primary configuration file for the plugin's core features, such as database settings, commands, and permission-based static tags.

**Location:** `game/csgo/addons/counterstrikesharp/configs/plugins/[Store] Tags/[Store] Tags.toml`

#### `[Database]`
This section contains the connection details for your MySQL database, which is used to store custom tag data.

```toml
[Database]
Host = "your_db_host"
Name = "your_db_name"
User = "your_db_user"
Pass = "your_db_password"
Port = 3306
```

#### `[Commands]`
Define the chat commands that players can use to open the tag management menu.

```toml
[Commands]
TagsMenu = ["tags", "tag"]
```

#### `[Tags.StaticTags]`
This is where you define permission-based tags that are **not** sold in the store. The plugin will check these tags in the order they appear in this file.

**The key for each tag is the permission string:**

- **By SteamID64:** `"76561199478674655"`
- **By Admin Flag:** `"@css/fondator"`
- **By Admin Group:** `"#admins"`

**Example:**

```toml
[Tags.StaticTags."@css/fondator"]
Name = "Fondator Tag"
Tag = "{red}[OWNER] "
ScoreboardTag = "[OWNER] "
ChatColor = "purple"
NameColor = "team"

[Tags.StaticTags."76561199478674655"]
Name = "Dev Tag"
Tag = "{purple}[DEV] "
ScoreboardTag = "[DEV] "
ChatColor = "purple"
NameColor = "team"
```
> **Note on Priority:** The plugin checks the `StaticTags` from top to bottom. The **first** tag a player has permission for will be considered their highest-priority default static tag.

---

### 2. Store Module Configuration (`Tags.json`)

This file defines the items that are registered and sold through the **Store API**. You edit this file to change the price, name, description, and duration of the tags available for purchase.

**Location:** `game/csgo/addons/counterstrikesharp/configs/plugins/StoreCore/StoreModules/Tags.json`

**Example `Tags.json`:**
```json
{
  "Category": "Tags",
  "Tags": {
    "1": {
      "Id": "premium_tag",
      "Name": "Premium",
      "ScoreboardTag": "[PREMIUM] ",
      "Tag": "[PREMIUM] ",
      "TagColor": "gold",
      "Description": "Tag color: Gold | NameColor: Team | ChatColor: Lime",
      "ChatColor": "lime",
      "NameColor": "team",
      "Price": 1000,
      "Duration": 86400,
      "Flags": ""
    },
    "2": {
      "Id": "custom_tag",
      "Name": "Custom Tag",
      "Tag": "",
      "ScoreboardTag": "",
      "TagColor": "",
      "Description": "Create your own unique tag!",
      "ChatColor": "",
      "NameColor": "",
      "Price": 5000,
      "Duration": 2592000,
      "Flags": "vip_only_flag"
    }
  }
}
```

## The Multi-Tag System Explained

This plugin's unique feature is the ability to combine tags. A player can have up to **two** tags equipped simultaneously.

### How it Works
-   The `!tags` menu acts as a **toggle system**. It shows all tags a player has access to (both from the store and from permissions).
-   Players can click on tags in the menu to turn them on (✔) or off.
-   If a player equips two tags, they will be combined in chat with a `+` separator.

**Example Display:**
If a player equips the "Premium" tag from the store and the "Owner" tag from permissions, their chat message will look like this:

`*DEAD* [ALL] [OWNER] + [PREMIUM] PlayerName: Message`

### Color Precedence
When multiple tags are equipped, the colors for the **player's name** and **chat message** are determined by the tag that was equipped **last**.

## Custom Tag Feature

The "Custom Tag" is a special item that can be sold in the store, allowing players to create their own personalized tag.

### How it Works
1.  **Purchase:** A player buys the "Custom Tag" item from the store.
2.  **Menu Opens:** Immediately after purchase, a special menu from `T3MenuSharedApi` opens.
3.  **Creation:** The player is prompted to enter four pieces of information:
    -   Their desired tag text (e.g., `[MyTag]`).
    -   The color for the tag text (e.g., `red`).
    -   The color for their player name (e.g., `lime` or `team`).
    -   The color for their chat messages (e.g., `default`).
4.  **Preview & Confirm:** The player can preview their creation in chat before confirming.
5.  **Save:** Once confirmed, the custom tag is saved to the database for that player and is automatically equipped. It remains theirs until the purchased item expires.

## Commands

| Command    | Description                               |
| ---------- | ----------------------------------------- |
| `!tags`    | Opens the interactive menu to manage your tags. |
| `!tag`     | An alias for the `!tags` command.         |

## Full Configuration Example

Here is a full example of a `[Store] Tags.toml` file that you can use as a starting point.

```toml
[Database]
Host = ""
Name = ""
User = ""
Pass = ""
Port = 3306

[Commands]
TagsMenu = ["tags", "tag"]

[Tags.StaticTags."76561199478674655"]
Name = "Dev Tag"
Tag = "{purple}[DEV] "
ScoreboardTag = "[DEV] "
ChatColor = "purple"
NameColor = "team"

[Tags.StaticTags."#fondator"]
Name = "Fondator Tag"
Tag = "{red}[OWNER] "
ScoreboardTag = "[OWNER] "
ChatColor = "purple"
NameColor = "team"

[Tags.StaticTags."#administrator"]
Name = "Admin Tag"
Tag = "{purple}[ADMIN] "
ScoreboardTag = "[ADMIN] "
ChatColor = "green"
NameColor = "team"

[Tags.StaticTags."#moderator"]
Name = "Mod Tag"
Tag = "{green}[MOD] "
ScoreboardTag = "[MOD] "
ChatColor = "lime"
NameColor = "team"

[Tags.StaticTags."#helper"]
Name = "Helper Tag"
Tag = "{orange}[HELPER] "
ScoreboardTag = "[HELPER] "
ChatColor = "green"
NameColor = "team"
```
