﻿using System.Collections.Generic;
using System.IO;

using kRPG.Enums;
using kRPG.GameObjects.Items.Weapons.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace kRPG.Packets
{
    public static class BowInitPacket
    {
        public static void Read(BinaryReader reader)
        {
            if (Main.netMode == 1)
            {
                int itemId = reader.ReadInt32();
                RangedWeapon bow = (RangedWeapon) Main.item[itemId].modItem;
                bow.dps = reader.ReadSingle();
                bow.enemyDef = reader.ReadInt32();
                bow.Initialize();
            }
        }

        public static void Write(int whoAmI, float dps, int enemyDef)
        {
            if (Main.netMode == 2)
            {
                ModPacket packet = kRPG.Mod.GetPacket();
                packet.Write((byte) Message.BowInit);
                packet.Write(whoAmI);
                packet.Write(dps);
                packet.Write(enemyDef);
                packet.Send();
            }
        }
    }
}