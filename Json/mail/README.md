# Mail Configuration Guide

This directory contains mail configuration templates for automated item and gold transfers to a bank alt.

## Quick Start

### Method 1: Inline Configuration (Recommended for beginners)

Add the `MailConfig` object directly to your class profile JSON:

```json
{
  "ClassName": "Warrior",
  "Mode": "Grind",
  "Mail": true,
  "MailConfig": {
    "RecipientName": "YourBankAlt",
    "MinimumGoldToKeep": 20000,
    "MinimumItemQuality": 2,
    "SendGold": true,
    "SendItems": true,
    "ExcludedItemIds": [6948, 5956]
  }
}
```

### Method 2: External File Reference (Recommended for multiple profiles)

Reference a shared mail config file:

```json
{
  "ClassName": "Warrior",
  "Mode": "Grind",
  "Mail": true,
  "MailFilename": "bank_alt_standard.json"
}
```

## Available Templates

| File | Description | Use Case |
|------|-------------|----------|
| `template_mail.json` | General template | Copy and customize |
| `bank_alt_standard.json` | Standard banking setup | Level 60 grinding, 2g reserve |
| `low_level_grind.json` | Low-level grinding | Level 1-30, 1g reserve |
| `gold_farming.json` | Gold farming optimization | Level 60 farms, 5g reserve, blue+ items only |

## Configuration Properties

### RecipientName
**Type:** `string`
**Example:** `"Bankonelol"`

Character name to send mail to. Can be left empty if using the `MAIL_RECIPIENT` environment variable.

**Environment Variable Override:**
```bash
set MAIL_RECIPIENT=AnotherBankAlt
```

### MinimumGoldToKeep
**Type:** `int` (copper)
**Default:** `10000` (1 gold)
**Examples:**
- `10000` = 1 gold
- `20000` = 2 gold
- `50000` = 5 gold

Gold above this threshold will be sent to the recipient.

### MinimumItemQuality
**Type:** `int`
**Default:** `0`
**Values:**
- `0` = Grey (Poor)
- `1` = White (Common)
- `2` = Green (Uncommon)
- `3` = Blue (Rare)
- `4` = Purple (Epic)
- `5` = Orange (Legendary)

Items at or above this quality will be sent.

### SendGold
**Type:** `bool`
**Default:** `true`

Whether to send gold above the `MinimumGoldToKeep` threshold.

### SendItems
**Type:** `bool`
**Default:** `true`

Whether to send items meeting the quality threshold.

### ExcludedItemIds
**Type:** `int[]`
**Default:** `[]`

Item IDs to never mail, even if they meet the quality threshold.

**Common Exclusions:**
- `6948` — Hearthstone
- `5956` — Blacksmith Hammer
- `7005` — Skinning Knife
- `2901` — Mining Pick
- `4238` — Apprentice's Shirt
- `3275` — Flint and Tinder
- `1760` — Copper Rod
- `13446` — Major Healing Potion
- `18564` — Bindings of the Windseeker (Left)
- `22577` — Empowered Leggings

## How Mail Works

### Activation Requirements

Mail is sent automatically when the bot visits a mailbox **AND**:
1. Recently vendored or repaired (prevents mail spam)
2. Has items to mail OR excess gold

### NPC Sequence Integration

Mail action is typically configured in the `NPC.Sequence` section:

```json
{
  "NPC": {
    "Sequence": [
      {
        "Cost": 6,
        "Name": "Repair",
        "Key": "C",
        "Requirement": "Durability% < 35"
      },
      {
        "Cost": 6,
        "Name": "Sell",
        "Key": "C",
        "Requirements": ["BagFull", "BagGreyItem"]
      },
      {
        "Cost": 6.5,
        "Name": "Mail",
        "Requirements": [
          "VendoredOrRepairedRecently",
          "HasMailableItems || HasExcessGold"
        ]
      }
    ]
  }
}
```

**Requirements Explained:**
- `VendoredOrRepairedRecently` — Prevents mail spam (only mail after vendor/repair)
- `HasMailableItems` — Has items meeting quality threshold
- `HasExcessGold` — Has gold above `MinimumGoldToKeep`

### Cost System

The `Cost` value (6.5) is higher than vendor/repair (6.0) to ensure mail happens last:
- Lower cost = higher priority
- Bot chooses lowest cost action first
- Mail should have highest cost to run after vendor/repair

## Examples

### Example 1: Conservative Bank Alt (Keep 5 gold, send blue+ items)

```json
{
  "RecipientName": "MyBank",
  "MinimumGoldToKeep": 50000,
  "MinimumItemQuality": 3,
  "SendGold": true,
  "SendItems": true,
  "ExcludedItemIds": [6948, 5956, 13446]
}
```

### Example 2: Aggressive Farming (Keep 1 gold, send green+ items)

```json
{
  "RecipientName": "MyBank",
  "MinimumGoldToKeep": 10000,
  "MinimumItemQuality": 2,
  "SendGold": true,
  "SendItems": true,
  "ExcludedItemIds": [6948, 5956]
}
```

### Example 3: Gold Only (No items)

```json
{
  "RecipientName": "MyBank",
  "MinimumGoldToKeep": 20000,
  "MinimumItemQuality": 5,
  "SendGold": true,
  "SendItems": false,
  "ExcludedItemIds": []
}
```

### Example 4: Items Only (No gold)

```json
{
  "RecipientName": "MyBank",
  "MinimumGoldToKeep": 0,
  "MinimumItemQuality": 2,
  "SendGold": false,
  "SendItems": true,
  "ExcludedItemIds": [6948, 5956]
}
```

## Troubleshooting

### Mail not being sent

**Check:**
1. `"Mail": true` is set in class profile
2. Mail config has valid `RecipientName`
3. NPC sequence includes Mail action with correct requirements
4. Bot recently vendored or repaired (triggers `VendoredOrRepairedRecently`)
5. Items meet the `MinimumItemQuality` threshold

### Wrong items being sent

**Check:**
1. `MinimumItemQuality` is set correctly (2 = green, 3 = blue)
2. Item IDs are in `ExcludedItemIds` if you want to keep them
3. Review item quality values (white/grey items might be sent if quality is 0 or 1)

### Recipient name not working

**Options:**
1. Set `RecipientName` directly in JSON
2. Use environment variable: `set MAIL_RECIPIENT=BankAlt`
3. Environment variable takes precedence over JSON value

## Performance Notes

- Mail UI interaction adds ~3-5 seconds per visit
- Mail is rate-limited by `VendoredOrRepairedRecently` requirement
- Typical frequency: once every 30-60 minutes (when bags fill up)

## Security

**⚠️ Warning:** The recipient name is stored in plain text in JSON files. Do not commit profiles with your actual character names to public repositories if privacy is a concern.

**Best Practice:** Use the `MAIL_RECIPIENT` environment variable instead of storing the name in JSON.

## Advanced Configuration

### Per-Class Profiles

Create class-specific mail configs for different farming strategies:

```
Json/mail/
  ├── warrior_aoe_farming.json  (Keep 5g, send blue+)
  ├── mage_aoe_farming.json     (Keep 10g, send green+)
  ├── rogue_pickpocket.json     (Keep 2g, send all items)
  └── hunter_leveling.json      (Keep 1g, send green+)
```

Reference in class profile:
```json
{
  "ClassName": "Warrior",
  "MailFilename": "warrior_aoe_farming.json"
}
```

### Shared Across Multiple Characters

Use the same mail config file for all your grinding characters:

```json
{
  "MailFilename": "bank_alt_standard.json"
}
```

Update `bank_alt_standard.json` once, all profiles inherit the change.

---

**Related Files:**
- `Core/Mail/MailConfiguration.cs` — Configuration model
- `Core/Mail/MailGoal.cs` — GOAP goal implementation
- `Frontend/Pages/Mail.razor` — UI for mail management

**Environment Variable:**
- `MAIL_RECIPIENT` — Overrides `RecipientName` from JSON (see `MailConfiguration.cs:21`)
