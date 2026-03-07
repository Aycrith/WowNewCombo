using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core.Database;

/// <summary>
/// Hard-coded vendor locations for fallback when auto-search fails or returns incorrect positions.
/// Used as redundancy when pathfinding to auto-discovered NPCs encounters issues.
/// </summary>
public static class VendorLocations
{
    /// <summary>
    /// Represents a vendor with known location and capabilities
    /// </summary>
    public sealed class VendorInfo
    {
        public required string Name { get; init; }
        public required Vector3 WorldPosition { get; init; }
        public required bool CanRepair { get; init; }
        public required bool CanSell { get; init; }
        public required int Priority { get; init; } // Lower = preferred
        public string? Notes { get; init; }
    }

    /// <summary>
    /// Vendors organized by zone name for easy lookup
    /// </summary>
    public static readonly Dictionary<string, List<VendorInfo>> VendorsByZone = new()
    {
        // ===== BLOOD ELF STARTING ZONES (Classic/TBC) =====
        
        ["Eversong Woods"] = new()
        {
            new VendorInfo
            {
                Name = "Keelen Sheets",
                WorldPosition = new(10387.5f, -6321.36f, 35.58f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Falconwing Square - General Goods"
            },
            new VendorInfo
            {
                Name = "Skybreaker Voren",
                WorldPosition = new(10315.7f, -6373.81f, 38.62f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Falconwing Square - Armor & Weapons"
            },
            new VendorInfo
            {
                Name = "Eralan",
                WorldPosition = new(10342.2f, -6363.47f, 38.62f),
                CanRepair = true,
                CanSell = true,
                Priority = 3,
                Notes = "Falconwing Square - Trade Supplies"
            },
            new VendorInfo
            {
                Name = "Halis Dawnstrider",
                WorldPosition = new(9361.55f, -6777.95f, 15.97f),
                CanRepair = true,
                CanSell = true,
                Priority = 4,
                Notes = "Fairbreeze Village - General Goods"
            }
        },

        ["Ghostlands"] = new()
        {
            new VendorInfo
            {
                Name = "Provisioner Vredigar",
                WorldPosition = new(7597.84f, -6820.51f, 86.45f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Tranquillien - General Goods"
            },
            new VendorInfo
            {
                Name = "Blacksmith Frances",
                WorldPosition = new(7568.23f, -6895.12f, 86.45f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Tranquillien - Armor & Weapons"
            }
        },

        // ===== HUMAN STARTING ZONES =====

        ["Elwynn Forest"] = new()
        {
            new VendorInfo
            {
                Name = "Milly Osworth",
                WorldPosition = new(-9455.24f, 45.87f, 56.95f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Northshire Abbey - Trade Goods"
            },
            new VendorInfo
            {
                Name = "William Pestle",
                WorldPosition = new(-8830.76f, 858.19f, 99.03f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Goldshire - General Goods"
            },
            new VendorInfo
            {
                Name = "Corina Steele",
                WorldPosition = new(-8799.15f, 865.26f, 99.03f),
                CanRepair = true,
                CanSell = true,
                Priority = 3,
                Notes = "Goldshire - Blacksmith"
            }
        },

        // ===== DWARF STARTING ZONES =====

        ["Dun Morogh"] = new()
        {
            new VendorInfo
            {
                Name = "Tharek Blackstone",
                WorldPosition = new(-6230.78f, 331.03f, 383.21f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Anvilmar - Blacksmith"
            },
            new VendorInfo
            {
                Name = "Innkeeper Belm",
                WorldPosition = new(-5603.76f, -531.32f, 399.66f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Kharanos - Innkeeper & General Goods"
            }
        },

        // ===== NIGHT ELF STARTING ZONES =====

        ["Teldrassil"] = new()
        {
            new VendorInfo
            {
                Name = "Alanna Raveneye",
                WorldPosition = new(10523.4f, 788.63f, 1329.68f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Shadowglen - General Goods"
            },
            new VendorInfo
            {
                Name = "Innkeeper Keldamyr",
                WorldPosition = new(9821.55f, 963.74f, 1308.14f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Dolanaar - Innkeeper"
            }
        },

        // ===== ORC/TROLL STARTING ZONES =====

        ["Durotar"] = new()
        {
            new VendorInfo
            {
                Name = "Duokna",
                WorldPosition = new(-595.19f, -4199.62f, 38.27f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Valley of Trials - General Goods"
            },
            new VendorInfo
            {
                Name = "Trak'gen",
                WorldPosition = new(-837.03f, -4896.54f, 19.67f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Sen'jin Village - General Goods"
            },
            new VendorInfo
            {
                Name = "Tai'tasi",
                WorldPosition = new(272.19f, -4713.97f, 11.79f),
                CanRepair = true,
                CanSell = true,
                Priority = 3,
                Notes = "Razor Hill - General Goods"
            }
        },

        // ===== UNDEAD STARTING ZONES =====

        ["Tirisfal Glades"] = new()
        {
            new VendorInfo
            {
                Name = "Joshua Kien",
                WorldPosition = new(1860.85f, 1571.71f, 94.31f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Deathknell - Trade Goods"
            },
            new VendorInfo
            {
                Name = "Innkeeper Renee",
                WorldPosition = new(2246.97f, 241.88f, 34.11f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Brill - Innkeeper"
            }
        },

        // ===== TAUREN STARTING ZONES =====

        ["Mulgore"] = new()
        {
            new VendorInfo
            {
                Name = "Grull Hawkwind",
                WorldPosition = new(-2913.93f, -266.06f, 53.91f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Camp Narache - General Goods"
            },
            new VendorInfo
            {
                Name = "Mahnott Roughwound",
                WorldPosition = new(-2338.22f, -359.13f, -8.96f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Bloodhoof Village - General Goods"
            }
        },

        // ===== GNOME STARTING ZONES =====

        ["Dun Morogh Gnome"] = new()
        {
            new VendorInfo
            {
                Name = "Adlin Pridedrift",
                WorldPosition = new(-6082.23f, 386.87f, 395.61f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Coldridge Valley - General Goods"
            }
        },

        // ===== DRAENEI STARTING ZONES (TBC) =====

        ["Azuremyst Isle"] = new()
        {
            new VendorInfo
            {
                Name = "Arred",
                WorldPosition = new(-4077.13f, -13742.9f, 73.68f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Ammen Vale - General Goods"
            },
            new VendorInfo
            {
                Name = "Caregiver Chellan",
                WorldPosition = new(-3965.14f, -13931.3f, 100.62f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "Azure Watch - General Goods"
            }
        },

        // ===== MID-LEVEL ZONES (10-20) =====

        ["Westfall"] = new()
        {
            new VendorInfo
            {
                Name = "Quartermaster Lewis",
                WorldPosition = new(-10628.8f, 1037.45f, 34.28f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Sentinel Hill - General Goods"
            }
        },

        ["The Barrens"] = new()
        {
            new VendorInfo
            {
                Name = "Wrahk",
                WorldPosition = new(-441.87f, -2596.34f, 95.78f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "The Crossroads - General Goods"
            },
            new VendorInfo
            {
                Name = "Zargh",
                WorldPosition = new(-456.32f, -2652.69f, 95.78f),
                CanRepair = true,
                CanSell = true,
                Priority = 2,
                Notes = "The Crossroads - Blacksmith"
            }
        },

        ["Loch Modan"] = new()
        {
            new VendorInfo
            {
                Name = "Innkeeper Hearthstove",
                WorldPosition = new(-5409.13f, -2934.95f, 341.97f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Thelsamar - Innkeeper"
            }
        },

        ["Darkshore"] = new()
        {
            new VendorInfo
            {
                Name = "Barithras Moonshade",
                WorldPosition = new(6406.72f, 381.29f, 17.95f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Auberdine - General Goods"
            }
        },

        // ===== CONTESTED ZONES (20-30) =====

        ["Hillsbrad Foothills"] = new()
        {
            new VendorInfo
            {
                Name = "Christoph Jeffcoat",
                WorldPosition = new(-206.03f, -2109.87f, 106.69f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Tarren Mill - General Goods"
            }
        },

        ["Redridge Mountains"] = new()
        {
            new VendorInfo
            {
                Name = "Amy Davenport",
                WorldPosition = new(-9464.23f, -2149.87f, 68.67f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Lakeshire - General Goods"
            }
        },

        ["Stonetalon Mountains"] = new()
        {
            new VendorInfo
            {
                Name = "Maggran Earthbinder",
                WorldPosition = new(1063.19f, 1031.82f, 135.07f),
                CanRepair = true,
                CanSell = true,
                Priority = 1,
                Notes = "Sun Rock Retreat - General Goods"
            }
        }
    };

    /// <summary>
    /// Try to find vendors for a specific zone
    /// </summary>
    public static bool TryGetVendorsForZone(string zoneName, out List<VendorInfo>? vendors)
    {
        return VendorsByZone.TryGetValue(zoneName, out vendors);
    }

    /// <summary>
    /// Find the closest vendor to a given position from a vendor list
    /// </summary>
    public static VendorInfo? FindClosestVendor(List<VendorInfo> vendors, Vector3 playerPosition)
    {
        return FindClosestVendor(vendors, playerPosition, static _ => true);
    }

    public static VendorInfo? FindClosestVendor(List<VendorInfo> vendors, Vector3 playerPosition, Predicate<VendorInfo> predicate)
    {
        VendorInfo? closest = null;
        float closestDistance = float.MaxValue;

        foreach (var vendor in vendors)
        {
            if (!predicate(vendor))
            {
                continue;
            }

            float distance = Vector3.Distance(playerPosition, vendor.WorldPosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = vendor;
            }
        }

        return closest;
    }
}
