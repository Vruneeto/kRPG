﻿using System;
using System.Collections.Generic;
using System.Linq;
using kRPG.Buffs;
using kRPG.Enums;
using kRPG.GUI;
using kRPG.Items;
using kRPG.Items.Dusts;
using kRPG.Items.Glyphs;
using kRPG.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace kRPG
{
    public class PlayerCharacter : ModPlayer
    {
        public const int defaultMaxUpgradeLevel = 7;

        public ProceduralSpell[] abilities = new ProceduralSpell[4];
        public AbilitiesGUI abilitiesGUI;
        public int accuracy;

        public float accuracyCounter = 0.5f;
        public int activeInvPage;

        public Dictionary<ELEMENT, int> ailmentIntensity = new Dictionary<ELEMENT, int>
        {
            {ELEMENT.FIRE, 0}, {ELEMENT.COLD, 0}, {ELEMENT.LIGHTNING, 0}, {ELEMENT.SHADOW, 0}
        };

        public Dictionary<ELEMENT, Type> ailments = new Dictionary<ELEMENT, Type>
        {
            {ELEMENT.FIRE, typeof(Fire)}, {ELEMENT.COLD, typeof(Cold)}, {ELEMENT.LIGHTNING, typeof(Lightning)}, {ELEMENT.SHADOW, typeof(Shadow)}
        };

        public int allres;
        public AnvilGUI anvilGUI;

        public Dictionary<STAT, int> baseStats = new Dictionary<STAT, int>();
        public int bigCritCounter = 50;
        public int bigHitCounter = 50;

        public int bonusLife;
        public int bonusMana;
        public bool canHealMana = true;
        public List<ProceduralSpellProj> circlingProtection = new List<ProceduralSpellProj>();
        public float critAccuracyCounter = 0.5f;

        public int critBoost;
        public int critCounter = 50;
        public float critMultiplier = 1f;
        private float degenTimer;

        public Dictionary<ELEMENT, int> eleres = new Dictionary<ELEMENT, int>
        {
            {ELEMENT.FIRE, 0}, {ELEMENT.COLD, 0}, {ELEMENT.LIGHTNING, 0}, {ELEMENT.SHADOW, 0}
        };

        public int evasion = 2;
        public int evasionCounter = 50;

        public Dictionary<ELEMENT, bool> hasAilment = new Dictionary<ELEMENT, bool>
        {
            {ELEMENT.FIRE, false}, {ELEMENT.COLD, false}, {ELEMENT.LIGHTNING, false}, {ELEMENT.SHADOW, false}
        };

        private bool initialized;

        public Item[][] inventories = new Item[3][] {new Item[40], new Item[40], new Item[40]};
        public InventoryGui inventoryGUI;

        public double itemRotation;
        public Item lastSelectedWeapon;
        private int leechCooldown;

        public int level = 1;

        private int levelAnimation;
        public LevelGui levelGUI;
        public float lifeDegen;

        public float lifeLeech;

        public float lifeRegen = 1f;

        public int mana;
        public float manaRegen;
        private float manaRegenTimer;
        public List<ProceduralMinion> minions = new List<ProceduralMinion>();
        public int permanence;
        private float regenTimer;
        public Dictionary<RITUAL, bool> rituals = new Dictionary<RITUAL, bool>();
        public ProceduralSpell selectedAbility = null;
        public SpellCraftingGui spellCraftingGui;
        public List<SpellEffect> spellEffects = new List<SpellEffect>();
        public bool statPage = true;
        public StatusBar statusBar;
        public Dictionary<STAT, int> tempStats = new Dictionary<STAT, int>();
        public List<Trail> trails = new List<Trail>();
        public int transcendence;
        public int xp;

        public PlayerCharacter()
        {
            foreach (STAT stat in Enum.GetValues(typeof(STAT)))
            {
                baseStats[stat] = 0;
                tempStats[stat] = 0;
            }

            permanence = 0;
            transcendence = 0;
            for (int i = 0; i < abilities.Length; i += 1)
            {
                abilities[i] = new ProceduralSpell(mod);
                for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                {
                    abilities[i].glyphs[j] = new Item();
                    abilities[i].glyphs[j].SetDefaults(0, true);
                }
            }

            abilities[0].key = Keys.Z;
            abilities[1].key = Keys.X;
            abilities[2].key = Keys.C;
            abilities[3].key = Keys.V;

            inventories = new Item[3][];
            for (int i = 0; i < inventories.Length; i += 1)
            {
                inventories[i] = new Item[40];
                for (int j = 0; j < inventories[i].Length; j += 1)
                {
                    inventories[i][j] = new Item();
                    inventories[i][j].SetDefaults(0, true);
                }
            }
        }

        public float critHitChance
        {
            get
            {
                float diff = 4f + level / 12f;
                return 1f - diff * (1f - 0.8f) / (accuracy + diff);
            }
        }

        public float damageMultiplier => 1f + TotalStats(STAT.POTENCY) * 0.05f + Math.Min(0.09f, TotalStats(STAT.POTENCY) * 0.06f);

        public float hitChance
        {
            get
            {
                float diff = 7f + level / 40f;
                return 1f - diff * (1f - 0.85f) / (accuracy + diff);
            }
        }

        public int pointsAllocated
        {
            get { return Enum.GetValues(typeof(STAT)).Cast<STAT>().Sum(stat => baseStats[stat]); }
        }

        public Dictionary<ELEMENT, int> resistance
        {
            get
            {
                Dictionary<ELEMENT, int> dict = new Dictionary<ELEMENT, int>();
                foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                    dict[element] = eleres[element] + allres;
                return dict;
            }
        }

        public void AddXp(int xp)
        {
            if (Main.gameMenu) return;
            if (xp == 0) return;
            this.xp += xp;

            Check:
            if (this.xp >= ExperienceToLevel())
            {
                this.xp -= ExperienceToLevel();
                LevelUp();
                goto Check;
            }

            CombatText.NewText(player.getRect(), new Color(127, 159, 255), xp + " XP");
        }

        public float DamageMultiplier(ELEMENT? element, bool melee, bool ranged = false, bool magic = false, bool thrown = false, bool minion = false)
        {
            float dmgModifier = 1f;
            if (melee) dmgModifier *= player.meleeDamage;
            if (ranged) dmgModifier *= player.rangedDamage;
            if (magic) dmgModifier *= player.magicDamage;
            if (thrown) dmgModifier *= player.thrownDamage;
            if (minion) dmgModifier *= player.minionDamage;
            return dmgModifier;
        }

        public override void DrawEffects(PlayerDrawInfo drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (Main.netMode == 2 || Main.myPlayer != player.whoAmI) return;
            if (player.statLife < 1) return;
            if (hasAilment[ELEMENT.FIRE])
            {
                if (Main.rand.Next(2) == 0)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Fire, player.velocity.X * 0.4f,
                        player.velocity.Y * 0.4f, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 1.8f;
                    Main.dust[dust].velocity.Y -= 0.5f;
                }

                Lighting.AddLight(player.position, 0.7f, 0.4f, 0.1f);
                fullBright = true;
            }

            if (hasAilment[ELEMENT.COLD])
            {
                if (Main.rand.Next(2) == 0)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, ModContent.GetInstance<Ice>().Type,
                        player.velocity.X, player.velocity.Y, 100, Color.White, 1.5f);
                    Main.dust[dust].noGravity = true;
                }

                Lighting.AddLight(player.position, 0f, 0.4f, 1f);
            }

            if (hasAilment[ELEMENT.LIGHTNING])
            {
                if (Main.rand.Next(2) == 0)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Electric, player.velocity.X,
                        player.velocity.Y, 100, default, 0.5f);
                    Main.dust[dust].noGravity = true;
                }

                Lighting.AddLight(player.position, 0.5f, 0.5f, 0.5f);
                fullBright = true;
            }

            if (hasAilment[ELEMENT.SHADOW])
                if (Main.rand.Next(3) < 2)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Shadowflame, player.velocity.X,
                        player.velocity.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }

            if (Main.netMode == 2) return;
            SpriteBatch spriteBatch = Main.spriteBatch;

            foreach (Trail trail in trails.ToArray())
                trail.Draw(spriteBatch, player);

            if (levelAnimation >= 60)
                return;
            if (levelAnimation < 24)
            {
                fullBright = true;
                Lighting.AddLight(player.position, 0.9f, 0.9f, 0.9f);
            }
            else
            {
                Lighting.AddLight(player.position, 0.4f, 0.4f, 0.4f);
            }

            spriteBatch.Draw(GFX.levelUp, player.Bottom - new Vector2(48, 108) - Main.screenPosition, new Rectangle(0, levelAnimation / 3 * 96, 96, 96),
                Color.White);
            levelAnimation += 1;
        }

        public int ExperienceToLevel()
        {
            if (level < 5)
                return 80 + level * 20;
            if (level < 10)
                return level * 40;
            if (level < 163)
                return (int) (280 * Math.Pow(1.09, level - 5) + 3 * level);
            return (int) (2000000000 - 288500000000 / level);
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            foreach (Projectile projectile in Main.projectile)
                if (projectile.modProjectile is ProceduralSpear || projectile.modProjectile is ProceduralMinion)
                    projectile.Kill();
            foreach (ProceduralSpellProj spell in circlingProtection)
                spell.projectile.Kill();
            circlingProtection.Clear();
        }

        private void LeechLife(Item item, int damage)
        {
            if (leechCooldown != 0)
                return;
            int leechAmount = Math.Min((int) (damage * lifeLeech), (int) (player.inventory[player.selectedItem].damage / 2 * (1 + lifeLeech)));
            leechAmount = Math.Min(leechAmount, (int) (player.statLifeMax2 * lifeLeech * 0.2));
            if (leechAmount > 1)
            {
                player.statLife += leechAmount;
                player.HealEffect(leechAmount);
                leechCooldown = item.useAnimation * 3;
            }

            else if (lifeLeech > 0f)
            {
                player.statLife += 1;
                player.HealEffect(1);
                leechCooldown = (int) (item.useAnimation * (3 - Math.Min(1.4f, lifeLeech * 10f)));
            }
        }

        public void LevelUp()
        {
            level += 1;
            if (!Main.gameMenu) GFX.sfxLevelUp.Play(0.5f * Main.soundVolume, 0f, 0f);
            if (Main.netMode == 1)
            {
                ModPacket packet = mod.GetPacket();
                packet.Write((byte) Message.SyncLevel);
                packet.Write(player.whoAmI);
                packet.Write(level);
                packet.Send();
            }

            levelAnimation = 0;
            Main.NewText("Congratulations! You are now level " + level, 255, 223, 63);
        }

        private void ModifyDamage(ref int damage, ref bool crit, NPC target, Item item = null, Projectile proj = null)
        {
            if (rituals[RITUAL.WARRIOR_OATH])
            {
                crit = false;
                float damageBoost = 1f + TotalStats(STAT.RESILIENCE) * 0.04f;
                damageBoost += Math.Min(0.1f, TotalStats(STAT.RESILIENCE) * 0.02f);
                damage = (int) (damage * damageBoost);
            }

            Dictionary<ELEMENT, int> eleDmg = new Dictionary<ELEMENT, int>();

            if (item != null)
            {
                kItem ki = item.GetGlobalItem<kItem>();
                damage += ki.GetEleDamage(item, player);
                eleDmg = ki.GetIndividualElements(item, player);
            }
            else if (proj != null)
            {
                kProjectile kp = proj.GetGlobalProjectile<kProjectile>();
                damage += kp.GetEleDamage(proj, player);
                eleDmg = kp.GetIndividualElements(proj, player);
            }

            if (hasAilment[ELEMENT.SHADOW])
                damage = Math.Min(damage * 2 / 5, damage - ailmentIntensity[ELEMENT.SHADOW]);
            //    damage = damage * (20 + 9360 / (130 + ailmentIntensity[ELEMENT.SHADOW])) / 100;

            kNPC victim = target.GetGlobalNPC<kNPC>();

            if (!crit && Main.netMode == 0)
                crit = Main.rand.Next(500) < 50 + victim.ailmentIntensity[ELEMENT.COLD];

            if (crit)
            {
                damage = (int) (damage / damageMultiplier * (damageMultiplier + critMultiplier));
                if (rituals[RITUAL.ELDRITCH_FURY])
                {
                    int i = damage;
                    int c = target.boss ? 7 : 2;
                    damage += Math.Min(mana * c, i);
                    mana = Math.Max(0, mana - i / c);
                }
            }

            if (item == null && proj == null) return;

            if (victim.hasAilment[ELEMENT.LIGHTNING])
                damage += 1 + victim.ailmentIntensity[ELEMENT.LIGHTNING];

            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
            {
                if (Main.rand.Next(target.boss ? 500 : 200) >= 30 + eleDmg[element])
                    continue;
                if (eleDmg[element] <= 0)
                    continue;
                Type t = ailments[element];
                ModBuff buff;
                if (ailments[element] == typeof(Fire))
                    buff = ModContent.GetInstance<Fire>();
                else if (ailments[element] == typeof(Cold))
                    buff = ModContent.GetInstance<Cold>();
                else if (ailments[element] == typeof(Lightning))
                    buff = ModContent.GetInstance<Lightning>();
                else
                    buff = ModContent.GetInstance<Shadow>();
                target.AddBuff(buff.Type, target.boss ? 30 + Math.Min(eleDmg[element], 30) * 3 : 120 + Math.Min(eleDmg[element], 15) * 12);
                victim.ailmentIntensity[element] = target.boss ? eleDmg[element] / 2 : eleDmg[element];
                victim.hasAilment[element] = true;
            }
        }

        public void ModifyDamageTakenFromNPC(ref int damage, ref bool crit, Dictionary<ELEMENT, int> eleDmg)
        {
            double dmg = 0.5 * Math.Pow(damage, 1.35);
            Dictionary<ELEMENT, int> originalEle = eleDmg;
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                eleDmg[element] = (int) (0.5 * Math.Pow(eleDmg[element], 1.35));
            if (!Main.expertMode)
            {
                dmg = dmg * 1.3;
                foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                    eleDmg[element] = (int) (eleDmg[element] * 1.3);
            }

            damage = (int) Math.Round(Math.Min(dmg, (double) damage * 3));
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                eleDmg[element] = Math.Min(originalEle[element] * 3, eleDmg[element]);
            bool bossfight = false;
            foreach (NPC n in Main.npc)
                if (n.active)
                    if (n.boss)
                        bossfight = true;
            int elecount = Enum.GetValues(typeof(ELEMENT)).Cast<ELEMENT>().Count(element => eleDmg[element] > 0);
            if (elecount > 0) damage = (int) Math.Round(damage * (kNPC.ELE_DMG_MODIFIER + 1) / 2);
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
            {
                damage -= Math.Min(resistance[element], eleDmg[element] * 3 / 5);
                if (Main.rand.Next(player.statLifeMax2 + resistance[element] * 20) >= 15 + eleDmg[element] * (bossfight ? 2 : 8) || Main.netMode == 2)
                    continue;
                if (eleDmg[element] <= 0)
                    continue;
                Type t = ailments[element];
                ModBuff buff;
                if (ailments[element] == typeof(Fire))
                    buff = ModContent.GetInstance<Fire>();
                else if (ailments[element] == typeof(Cold))
                    buff = ModContent.GetInstance<Cold>();
                else if (ailments[element] == typeof(Lightning))
                    buff = ModContent.GetInstance<Lightning>();
                else
                    buff = ModContent.GetInstance<Shadow>();
                player.AddBuff(buff.Type, bossfight ? 90 : 210);
                int intensity = eleDmg[element] * 3 / 2;
                ailmentIntensity[element] = Main.expertMode ? intensity * 2 / 3 : intensity;
                hasAilment[element] = true;
            }

            if (Main.rand.Next(player.statLifeMax2 + player.statDefense) < damage * 3)
                player.AddBuff(ModContent.BuffType<Physical>(), 15 + Math.Min(30, damage * 30 / player.statLifeMax2));
            if (hasAilment[ELEMENT.LIGHTNING])
                damage += 1 + ailmentIntensity[ELEMENT.LIGHTNING];
        }

        public override void ModifyDrawLayers(List<PlayerLayer> layers)
        {
            if (kRPG.overhaul != null)
                return;
            for (int i = 0; i < layers.Count; i += 1)
                if (layers[i].Name.Contains("Held"))
                    layers.Insert(i + 2, new PlayerLayer("kRPG", "ProceduralItem", drawinfo =>
                    {
                        if (player.itemAnimation <= 0)
                            return;
                        if (player.HeldItem.type == mod.GetItem("ProceduralStaff").item.type)
                        {
                            if (Main.gameMenu) return;

                            ProceduralStaff staff = (ProceduralStaff) player.HeldItem.modItem;

                            Vector2 pos = player.Center - Main.screenPosition;
                            staff.DrawHeld(drawinfo, Lighting.GetColor((int) (player.Center.X / 16f), (int) (player.Center.Y / 16f)),
                                player.itemRotation + (float) API.Tau * player.direction / 8, staff.item.scale, pos);
                        }
                        else if (player.HeldItem.type == mod.GetItem("ProceduralSword").item.type)
                        {
                            if (Main.gameMenu) return;

                            ProceduralSword sword = (ProceduralSword) player.HeldItem.modItem;

                            if (sword.spear) return;

                            Vector2 pos = player.Center - Main.screenPosition;
                            sword.DrawHeld(drawinfo, Lighting.GetColor((int) (player.Center.X / 16f), (int) (player.Center.Y / 16f)),
                                player.itemRotation + (float) API.Tau, sword.item.scale, pos);
                        }
                    }));
        }

        public override void ModifyHitByNPC(NPC npc, ref int damage, ref bool crit)
        {
            Dictionary<ELEMENT, int> dict = new Dictionary<ELEMENT, int>();
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                dict[element] = npc.GetGlobalNPC<kNPC>().elementalDamage[element];
            ModifyDamageTakenFromNPC(ref damage, ref crit, dict);
        }

        public override void ModifyHitByProjectile(Projectile proj, ref int damage, ref bool crit)
        {
            Dictionary<ELEMENT, int> dict = new Dictionary<ELEMENT, int>();
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                dict[element] = proj.GetGlobalProjectile<kProjectile>().GetIndividualElements(proj, player)[element];
            ModifyDamageTakenFromNPC(ref damage, ref crit, dict);
        }

        public override void ModifyHitNPC(Item item, NPC target, ref int damage, ref float knockback, ref bool crit)
        {
            ModifyDamage(ref damage, ref crit, target, item);
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref int damage, ref float knockback, ref bool crit, ref int hitDirection)
        {
            if (proj.modProjectile is ProceduralSpellProj)
                ModifyDamage(ref damage, ref crit, target, null, proj);
            else
                ModifyDamage(ref damage, ref crit, target, null, proj);
        }

        public override void OnHitNPC(Item item, NPC target, int damage, float knockback, bool crit)
        {
            LeechLife(item, damage);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, int damage, float knockback, bool crit)
        {
            Item item = player.inventory[player.selectedItem];
            LeechLife(item, damage);
            if (item.type == ModContent.ItemType<ProceduralStaff>())
            {
                ProceduralStaff staff = (ProceduralStaff) item.modItem;
                bool proceed = false;
                if (proj.type == item.shoot)
                    proceed = true;
                else if (proj.type == ModContent.ProjectileType<ProceduralSpellProj>())
                    proceed = ((ProceduralSpellProj) proj.modProjectile).source == null;
                if (proceed)
                    staff.ornament?.onHit?.Invoke(player, target, item, damage, crit);
            }
            else if (proj.type == ModContent.ProjectileType<ProceduralSpear>() && item.type == ModContent.ItemType<ProceduralSword>())
            {
                ProceduralSword spear = (ProceduralSword) item.modItem;
                spear.accent?.onHit?.Invoke(player, target, spear, damage, crit);
            }
        }

        public void OpenInventoryPage(int page)
        {
            for (int i = 0; i < 40; i += 1)
                player.inventory[i + 10] = inventories[page][i];
            activeInvPage = page;
            statPage = false;
            API.FindRecipes();
            for (int i = 0; i < 50; i += 1)
                if (player.inventory[i].type == 71 || player.inventory[i].type == 72 || player.inventory[i].type == 73 || player.inventory[i].type == 74)
                    player.DoCoins(i);
        }

        public override void PlayerConnect(Player player)
        {
            ModPacket packet = mod.GetPacket();
            packet.Write((byte) Message.SyncLevel);
            packet.Write(player.whoAmI);
            packet.Write(level);
            packet.Send();
        }

        public override void PostItemCheck()
        {
            if (Main.netMode == 1)
                if (player.whoAmI != Main.myPlayer)
                    return;

            try
            {
                Item item = player.inventory[player.selectedItem];
                if (item.type != ModContent.ItemType<ProceduralStaff>() || item.shoot > 0)
                    return;
                player.releaseUseItem = true;
                if (player.itemAnimation == 1 && item.stack > 0)
                {
                    if (player.whoAmI != Main.myPlayer && player.controlUseItem)
                    {
                        player.itemAnimation = (int) (item.useAnimation / PlayerHooks.TotalMeleeSpeedMultiplier(player, item));
                        player.itemAnimationMax = player.itemAnimation;
                        player.reuseDelay = (int) (item.reuseDelay / PlayerHooks.TotalUseTimeMultiplier(player, item));
                        if (item.UseSound != null)
                            Main.PlaySound(item.UseSound, player.Center);
                    }
                    else
                    {
                        player.itemAnimation = 0;
                    }
                }

                if (player.itemTime < 2)
                {
                    Vector2 pos = player.RotatedRelativePoint(player.MountedCenter);
                    Vector2 relativeMousePos = Main.MouseWorld - pos;
                    itemRotation = Math.Atan2(relativeMousePos.Y * player.direction, relativeMousePos.X * player.direction) - player.fullRotation;
                    NetMessage.SendData(13, -1, -1, null, player.whoAmI);
                    NetMessage.SendData(41, -1, -1, null, player.whoAmI);
                }

                float scaleFactor = 6f;
                if (player.itemAnimation > 0)
                    player.itemRotation = (float) itemRotation;
                player.itemLocation = player.MountedCenter;
                player.itemLocation += player.itemRotation.ToRotationVector2() * scaleFactor * player.direction;
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat(e.ToString());
            }
        }

        public override void PostUpdate()
        {
            switch (Main.netMode)
            {
                case 2:
                case 1 when Main.myPlayer != player.whoAmI:
                    return;
            }

            Item item = player.inventory[player.selectedItem];
            if (item.damage > 0 && (item.melee || !item.noMelee || item.modItem is ProceduralSword))
                lastSelectedWeapon = item;

            switch (item.modItem)
            {
                case ProceduralSword s:
                {
                    if (Main.itemTexture[item.type] != s.texture)
                        Main.itemTexture[item.type] = s.texture;
                    break;
                }
                case ProceduralStaff st:
                {
                    if (Main.itemTexture[ModContent.ItemType<ProceduralStaff>()] != st.texture)
                        Main.itemTexture[ModContent.ItemType<ProceduralStaff>()] = st.texture;
                    break;
                }
            }

            //for (int i = 0; i < 40; i += 1)
            //    inventories[activeInvPage][i] = player.inventory[i + 10];

            //API.FindRecipes();
        }

        public override void PostUpdateEquips()
        {
            if (!initialized)
            {
                InitializeGui();
                initialized = true;
            }

            UpdateStats();
            if (lifeRegen > 0 && !player.bleed && !player.onFire && !player.poisoned && !player.onFire2 && !player.venom && !player.onFrostBurn)
                regenTimer += 1f;
            if (regenTimer > 60f / lifeRegen)
            {
                player.statLife = Math.Min(player.statLife + (int) (regenTimer / (60f / lifeRegen)), player.statLifeMax2);
                regenTimer = regenTimer % (60f / lifeRegen);
            }

            if (lifeDegen > 0) degenTimer += 1f;
            if (degenTimer >= 20f && hasAilment[ELEMENT.FIRE])
            {
                int amount = (int) Math.Round(lifeDegen / 3, 1);
                player.statLife = player.statLife - amount;
                CombatText.NewText(new Rectangle((int) player.position.X, (int) player.position.Y, player.width, player.height), new Color(255, 95, 31),
                    amount);
                degenTimer = 0;
                if (player.statLife <= 0) player.KillMe(PlayerDeathReason.ByCustomReason(player.name + " burned to death."), amount, 0);
            }

            manaRegenTimer += 1f;

            if (Main.chatRelease && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && Main.netMode != 2)
                for (int i = 0; i < abilities.Length; i += 1)
                    if (abilities[i].CompleteSkill())
                    {
                        bool useable = true;
                        foreach (Item item in abilities[i].glyphs)
                        {
                            Glyph glyph = (Glyph) item.modItem;
                            if (!glyph.CanUse()) useable = false;
                        }

                        if (!Main.keyState.IsKeyDown(abilities[i].key) || !Main.keyState.IsKeyUp(Keys.LeftShift) || abilities[i].remaining != 0 || !useable ||
                            player.statMana < abilities[i].ManaCost(this))
                            continue;
                        if (Main.netMode != 2)
                            abilities[i].UseAbility(player, Main.MouseWorld);
                        player.statMana -= abilities[i].ManaCost(this);
                    }

            for (int i = 0; i < spellEffects.Count; i += 1)
                spellEffects[i].Update(this);

            if (Main.mapStyle == 0 && kConfig.configLocal.clientside.arpgMinimap) Main.mapStyle += 1;
        }

        public override bool PreHurt(bool pvp, bool quiet, ref int damage, ref int hitDirection, ref bool crit, ref bool customDamage, ref bool playSound,
            ref bool genGore, ref PlayerDeathReason damageSource)
        {
            bool enemyCrit = Main.rand.Next(5) == 0 && Main.netMode == 0;
            int max = 80;
            int diff = 52;

            if (TotalStats(STAT.QUICKNESS) > 0 && !rituals[RITUAL.STONE_ASPECT])
            {
                if (damage < (level + 10) * 5)
                {
                    evasionCounter += 100 - max + max * diff / (diff + evasion);
                    if (evasionCounter >= 100)
                    {
                        evasionCounter -= 100;
                        if (enemyCrit)
                        {
                            critCounter += 100 - max + max * diff / (diff + evasion);
                            if (critCounter >= 100)
                                critCounter -= 100;
                            else
                                enemyCrit = false;

                            if (enemyCrit) damage = (int) (damage * 1.65);
                        }
                    }

                    else
                    {
                        player.NinjaDodge(40);
                        return false;
                    }
                }
                else
                {
                    max = 90;
                    diff = 38;
                    bigHitCounter += 100 - max + max * diff / (diff + evasion);
                    if (bigHitCounter >= 100)
                    {
                        bigHitCounter -= 100;
                        if (enemyCrit)
                        {
                            bigCritCounter += 100 - max + max * diff / (diff + evasion + TotalStats(STAT.WITS) * 5);
                            if (bigCritCounter >= 100)
                                bigCritCounter -= 100;
                            else
                                enemyCrit = false;

                            if (enemyCrit) damage = (int) (damage * 1.3);
                        }
                    }

                    else
                    {
                        player.NinjaDodge(40 + TotalStats(STAT.WITS) * 5);
                        return false;
                    }
                }
            }

            if (rituals[RITUAL.MIND_FORTRESS])
            {
                int i = (int) Math.Round(damage * 0.25);
                if (mana > i)
                {
                    damage -= i;
                    mana -= i;
                }
                else
                {
                    damage -= mana;
                    mana = 0;
                }
            }

            return true;
        }

        public override void PreUpdate()
        {
            if (Main.chatRelease && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && Main.netMode != 2)
            {
                if (PlayerInput.Triggers.Current.QuickHeal)
                    if (!PlayerInput.Triggers.Old.QuickHeal)
                    {
                        player.ApiQuickHeal();
                        PlayerInput.Triggers.Old.QuickHeal = true;
                    }

                if (PlayerInput.Triggers.Current.QuickMana)
                    if (!PlayerInput.Triggers.Old.QuickMana)
                    {
                        player.ApiQuickMana();
                        PlayerInput.Triggers.Old.QuickMana = true;
                    }

                if (PlayerInput.Triggers.Current.QuickBuff)
                    if (!PlayerInput.Triggers.Old.QuickBuff)
                    {
                        player.ApiQuickBuff();
                        PlayerInput.Triggers.Old.QuickBuff = true;
                    }
            }

            int selectedBinding3 = player.QuicksRadial.SelectedBinding;
            player.QuicksRadial.Update();

            if (player.QuicksRadial.SelectedBinding == -1 || !PlayerInput.Triggers.JustReleased.RadialQuickbar ||
                PlayerInput.MiscSettingsTEMP.HotbarRadialShouldBeUsed)
                return;

            switch (player.QuicksRadial.SelectedBinding)
            {
                case 0:
                    player.ApiQuickHeal();
                    break;
                case 1:
                    player.ApiQuickBuff();
                    break;
                case 2:
                    player.ApiQuickMana();
                    break;
            }

            PlayerInput.Triggers.JustReleased.RadialQuickbar = false;
        }

        public override void PreUpdateBuffs()
        {
        }

        public override void ResetEffects()
        {
            foreach (STAT stat in Enum.GetValues(typeof(STAT)))
                tempStats[stat] = 0;
            evasion = 2;
            accuracy = 0;
            bonusLife = 0;
            bonusMana = 0;
            lifeRegen = 1;
            lifeDegen = 0;
            manaRegen = 0;
            canHealMana = true;

            critBoost = 0;
            critMultiplier = 0f;
            lifeLeech = 0f;
            allres = 0;

            if (leechCooldown > 0) leechCooldown--;

            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
            {
                eleres[element] = 0;
                if (!hasAilment[element]) ailmentIntensity[element] = 0;
                hasAilment[element] = false;
            }

            if (Main.netMode != 1 || (int) Main.time % 300 != 0)
                return;
            ModPacket packet = mod.GetPacket();
            packet.Write((byte) Message.SyncStats);
            packet.Write(player.whoAmI);
            packet.Write(level);
            packet.Write(baseStats[STAT.RESILIENCE]);
            packet.Write(baseStats[STAT.QUICKNESS]);
            packet.Write(baseStats[STAT.POTENCY]);
            packet.Write(baseStats[STAT.WITS]);
            packet.Send();
        }

        public override void SetupStartInventory(IList<Item> items, bool mediumcoreDeath)
        {
            Random rand = new Random();
            switch (rand.Next(8))
            {
                default:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TinBroadsword : ItemID.CopperBroadsword, true);
                    break;
                case 1:
                    items[0].SetDefaults(ItemID.Spear, true);
                    break;
                case 2:
                    items[0].SetDefaults(ItemID.WoodenBoomerang, true);
                    break;
                case 3:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TopazStaff : ItemID.AmethystStaff, true);
                    break;
                case 4:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TinBow : ItemID.CopperBow, true);
                    Item arrows = new Item();
                    arrows.SetDefaults(rand.Next(2) == 0 ? ItemID.FlamingArrow : ItemID.WoodenArrow, true);
                    arrows.stack = rand.Next(2) == 0 ? 150 : 200;
                    items.Add(arrows);
                    break;
                case 5:
                    items[0].SetDefaults(ItemID.Shuriken, true);
                    items[0].stack = rand.Next(2) == 0 ? 150 : 100;
                    Item knives = new Item();
                    knives.SetDefaults(rand.Next(2) == 0 ? ItemID.PoisonedKnife : ItemID.ThrowingKnife, true);
                    knives.stack = 50;
                    items.Add(knives);
                    break;
                case 6:
                    items[0].SetDefaults(ItemID.WoodYoyo, true);
                    break;
                case 7:
                    items[0].SetDefaults(ItemID.ChainKnife, true);
                    break;
            }

            items[1].SetDefaults(rand.Next(3) == 0 ? ItemID.TinPickaxe : rand.Next(2) == 0 ? ItemID.CactusPickaxe : ItemID.CopperPickaxe, true);
            items[1].GetGlobalItem<kItem>().Initialize(items[1]);
            items[2].SetDefaults(rand.Next(2) == 0 ? ItemID.TinAxe : ItemID.CopperAxe);
            items[2].GetGlobalItem<kItem>().Initialize(items[2]);

            Item star = new Item();
            star.SetDefaults(ModContent.ItemType<Star_Blue>(), true);
            Item cross = new Item();
            switch (rand.Next(4))
            {
                default:
                    cross.SetDefaults(ModContent.ItemType<Cross_Red>(), true);
                    break;
                case 1:
                    cross.SetDefaults(ModContent.ItemType<Cross_Orange>(), true);
                    break;
                case 2:
                    cross.SetDefaults(ModContent.ItemType<Cross_Yellow>(), true);
                    break;
                case 3:
                    cross.SetDefaults(ModContent.ItemType<Cross_Green>(), true);
                    break;
            }

            ((Glyph) cross.modItem).Randomize();
            Item moon = new Item();
            switch (rand.Next(5))
            {
                default:
                    moon.SetDefaults(ModContent.ItemType<Moon_Yellow>(), true);
                    break;
                case 1:
                    moon.SetDefaults(ModContent.ItemType<Moon_Green>(), true);
                    break;
                case 2:
                    moon.SetDefaults(ModContent.ItemType<Moon_Blue>(), true);
                    break;
                case 3:
                    moon.SetDefaults(ModContent.ItemType<Moon_Violet>(), true);
                    break;
                case 4:
                    moon.SetDefaults(ModContent.ItemType<Moon_Purple>(), true);
                    break;
            }

            ((Glyph) moon.modItem).Randomize();
            items.Add(star);
            items.Add(cross);
            items.Add(moon);
        }

        public int TotalStats(STAT stat)
        {
            if (rituals[RITUAL.DEMON_PACT] && stat == STAT.POTENCY)
                return baseStats[stat] + tempStats[stat] + baseStats[STAT.RESILIENCE];

            if (rituals[RITUAL.DEMON_PACT] && stat == STAT.RESILIENCE)
                return tempStats[stat];

            return baseStats[stat] + tempStats[stat];
        }

        public bool UnspentPoints()
        {
            return pointsAllocated < level - 1;
        }

        public void UpdateStats()
        {
            float lifeMultiplier = 1f + (player.statLifeMax - 100f) / 400f;
            int addedLife = player.statLifeMax2 - player.statLifeMax;
            player.statLifeMax2 += 115 + TotalStats(STAT.RESILIENCE) * 10 + level * 5 + bonusLife - player.statLifeMax;
            player.statLifeMax2 = (int) Math.Round(player.statLifeMax2 * lifeMultiplier) + addedLife;
            float manaMultiplier = 1f + (player.statManaMax - 20f) / 200f * 1.5f;
            int addedMana = player.statManaMax2 - player.statManaMax;
            player.statManaMax2 += 19 + level + bonusMana + TotalStats(STAT.WITS) * 3 - player.statManaMax;
            player.statManaMax2 = (int) Math.Round(player.statManaMax2 * manaMultiplier) + addedMana;
            player.statDefense += TotalStats(STAT.RESILIENCE);
            allres += TotalStats(STAT.WITS);
            evasion += TotalStats(STAT.QUICKNESS);
            accuracy += TotalStats(STAT.WITS);
            if (rituals[RITUAL.STONE_ASPECT]) player.statDefense += TotalStats(STAT.QUICKNESS);
            lifeRegen += TotalStats(STAT.RESILIENCE) * 0.3f + TotalStats(STAT.WITS) * 0.2f;
            if (hasAilment[ELEMENT.FIRE])
                lifeDegen = ailmentIntensity[ELEMENT.FIRE] / 2;
            manaRegen = player.statManaMax2 * 0.06f + TotalStats(STAT.WITS) * 0.6f;

            if (Main.netMode != 2 && Main.myPlayer == player.whoAmI)
            {
                if (mana < 0) mana = 0;
                if (player.statMana < 0) player.statMana = 0;
                if (player.statMana < mana)
                    mana = player.statMana;
                if (rituals[RITUAL.ELAN_VITAL] && mana < player.statManaMax2)
                {
                    if (player.statLife > player.statLifeMax2 * 0.4 + player.statManaMax2 - mana)
                    {
                        player.statLife -= player.statManaMax2 - mana;
                        mana = player.statManaMax2;
                    }
                    else if (player.statLife > player.statLifeMax2 * 0.4)
                    {
                        mana += (int) (player.statLife - player.statLifeMax2 * 0.4);
                        player.statLife = (int) (player.statLifeMax2 * 0.4);
                    }
                }

                if (player.statMana == player.statManaMax2 && mana == player.statMana - 1)
                    mana = player.statMana;
                else player.statMana = mana;
                if (manaRegenTimer > 60f / manaRegen)
                {
                    mana = Math.Min(mana + (int) (manaRegenTimer / (60f / manaRegen)), player.statManaMax2);
                    manaRegenTimer = manaRegenTimer % (60f / manaRegen);
                }

                player.statMana = mana;
            }

            critMultiplier += TotalStats(STAT.POTENCY) * 0.04f;
            lifeLeech += TotalStats(STAT.POTENCY) * 0.002f;
            lifeLeech += Math.Min(0.006f, TotalStats(STAT.POTENCY) * 0.002f);

            player.meleeDamage *= damageMultiplier;
            player.rangedDamage *= damageMultiplier;
            player.magicDamage *= damageMultiplier;
            player.minionDamage *= damageMultiplier;
            player.thrownDamage *= damageMultiplier;

            player.moveSpeed *= 1f + Math.Min(1.2f, TotalStats(STAT.QUICKNESS) * 0.02f + Math.Min(level * 0.005f, 0.5f));
            player.meleeSpeed *= 1f + TotalStats(STAT.QUICKNESS) * 0.01f;
            player.jumpSpeedBoost += Math.Min(5f, TotalStats(STAT.QUICKNESS) * 0.2f + Math.Min(level * 0.05f, 2f));

            critBoost += Math.Min(TotalStats(STAT.QUICKNESS), Math.Max(4, TotalStats(STAT.QUICKNESS) / 2 + 2));
            player.magicCrit += critBoost;
            player.meleeCrit += critBoost;
            player.rangedCrit += critBoost;
            player.thrownCrit += critBoost;
        }

        #region Saving and loading

        public override void Initialize()
        {
            baseStats = new Dictionary<STAT, int>();
            tempStats = new Dictionary<STAT, int>();

            permanence = 0;
            transcendence = 0;

            level = 1;
            xp = 0;

            foreach (STAT stat in Enum.GetValues(typeof(STAT)))
            {
                baseStats[stat] = 0;
                tempStats[stat] = 0;
            }

            inventories = new Item[3][];
            for (int i = 0; i < inventories.Length; i += 1)
            {
                inventories[i] = new Item[40];
                for (int j = 0; j < inventories[i].Length; j += 1)
                    inventories[i][j] = new Item();
            }

            rituals = new Dictionary<RITUAL, bool>();
            foreach (RITUAL rite in Enum.GetValues(typeof(RITUAL)))
                rituals[rite] = false;

            for (int i = 0; i < abilities.Length; i += 1)
            {
                abilities[i] = new ProceduralSpell(mod);
                for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                    abilities[i].glyphs[j] = new Item();
            }

            abilities[0].key = Keys.Z;
            abilities[1].key = Keys.X;
            abilities[2].key = Keys.C;
            abilities[3].key = Keys.V;
        }

        public void InitializeGui()
        {
            if (Main.netMode == 2) return;
            BaseGui.guiElements.Clear();
            anvilGUI = new AnvilGUI(this);
            levelGUI = new LevelGui(this, mod);
            statusBar = new StatusBar(this, mod) {guiActive = true};
            inventoryGUI = new InventoryGui(this);
            abilitiesGUI = new AbilitiesGUI {guiActive = true};
            spellCraftingGui = new SpellCraftingGui(mod /*, glyphs, this*/);
        }

        public void CloseGuIs()
        {
            anvilGUI.CloseGui();
            levelGUI.CloseGui();
            spellCraftingGui.CloseGui();
        }

        public override void OnEnterWorld(Player player)
        {
            InitializeGui();

            if (player.whoAmI == Main.myPlayer)
                kRPG.CheckForUpdates();
        }

        public override TagCompound Save()
        {
            TagCompound tagCompound = new TagCompound
            {
                {"level", level},
                {"xp", xp},
                {"baseRESILIENCE", baseStats[STAT.RESILIENCE]},
                {"baseQUICKNESS", baseStats[STAT.QUICKNESS]},
                {"basePOTENCY", baseStats[STAT.POTENCY]},
                {"baseWITS", baseStats[STAT.WITS]},
                {"RITUAL_DEMON_PACT", rituals[RITUAL.DEMON_PACT]},
                {"RITUAL_WARRIOR_OATH", rituals[RITUAL.WARRIOR_OATH]},
                {"RITUAL_ELAN_VITAL", rituals[RITUAL.ELAN_VITAL]},
                {"RITUAL_STONE_ASPECT", rituals[RITUAL.STONE_ASPECT]},
                {"RITUAL_ELDRITCH_FURY", rituals[RITUAL.ELDRITCH_FURY]},
                {"RITUAL_MIND_FORTRESS", rituals[RITUAL.MIND_FORTRESS]},
                {"RITUAL_BLOOD_DRINKING", rituals[RITUAL.BLOOD_DRINKING]},
                {"life", player.statLife},
                {"permanence", permanence},
                {"transcendence", transcendence}
            };

            try
            {
                for (int i = 0; i < abilities.Length; i += 1)
                {
                    if (abilities[i] == null) return tagCompound;
                    tagCompound.Add("abilities" + i + "_key", abilities[i].key.ToString());
                    for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                        if (abilities[i].glyphs[j] != null)
                            tagCompound.Add("ability" + i + j, ItemIO.Save(abilities[i].glyphs[j]));
                }
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Abilities :: " + e);
            }

            try
            {
                for (int i = 0; i < inventories.Length; i += 1)
                for (int j = 0; j < inventories[i].Length; j += 1)
                    tagCompound.Add("item" + i + j, ItemIO.Save(inventories[i][j]));
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Inventories :: " + e);
            }

            return tagCompound;
        }

        public override void Load(TagCompound tag)
        {
            try
            {
                level = tag.GetInt("level");
                xp = tag.GetInt("xp");
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Level&XP :: " + e);
            }

            try
            {
                foreach (STAT stat in Enum.GetValues(typeof(STAT)))
                    baseStats[stat] = tag.GetInt("base" + stat);
                foreach (RITUAL rite in Enum.GetValues(typeof(RITUAL)))
                    rituals[rite] = tag.GetBool("RITUAL_" + rite);
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Stats&Rituals :: " + e);
            }

            try
            {
                abilities = new ProceduralSpell[4];
                for (int i = 0; i < abilities.Length; i += 1)
                {
                    abilities[i] = new ProceduralSpell(mod);
                    abilities[i].key = (Keys) Enum.Parse(typeof(Keys), tag.GetString("abilities" + i + "_key"));
                    for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                        if (tag.ContainsKey("ability" + i + j))
                            abilities[i].glyphs[j] = ItemIO.Load(tag.GetCompound("ability" + i + j));
                }
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Abilities :: " + e);
            }

            try
            {
                for (int i = 0; i < inventories.Length; i += 1)
                for (int j = 0; j < inventories[i].Length; j += 1)
                    inventories[i][j] = ItemIO.Load(tag.GetCompound("item" + i + j));
                OpenInventoryPage(0);
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Inventory :: " + e);
            }

            try
            {
                player.statLife = tag.GetInt("life");
                permanence = tag.GetInt("permanence");
                transcendence = tag.GetInt("transcendence");

                mana = 10 + (level - 1) * 3;
            }
            catch (SystemException e)
            {
                ModLoader.GetMod("kRPG").Logger.InfoFormat("@Miscellaneous :: " + e);
            }
        }

        #endregion
    }
}