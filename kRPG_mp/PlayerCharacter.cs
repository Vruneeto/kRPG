﻿using System;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;
using System.Collections.Generic;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.ID;
using Terraria.DataStructures;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using kRPG_mp.GUI;
using kRPG_mp.Items;
using kRPG_mp.Projectiles;
using kRPG_mp.Items.Glyphs;

namespace kRPG_mp
{
    public class PlayerCharacter : ModPlayer
    {
        public const int defaultMaxUpgradeLevel = 7;

        public int level = 1;
        public int xp = 0;
        public int pointsAllocated
        {
            get
            {
                int points = 0;
                foreach (STAT stat in Enum.GetValues(typeof(STAT)))
                {
                    points += baseStats[stat];
                }
                return points;
            }
        }

        public Dictionary<STAT, int> baseStats = new Dictionary<STAT, int>();
        public Dictionary<STAT, int> tempStats = new Dictionary<STAT, int>();
        public Dictionary<RITUAL, bool> rituals = new Dictionary<RITUAL, bool>();
        /*public Dictionary<ELEMENT, Type> ailments = new Dictionary<ELEMENT, Type>()
        {
            {ELEMENT.FIRE, typeof(Fire)},
            {ELEMENT.COLD, typeof(Cold)},
            {ELEMENT.LIGHTNING, typeof(Lightning)},
            {ELEMENT.SHADOW, typeof(Shadow)}
        };
        public Dictionary<ELEMENT, bool> hasAilment = new Dictionary<ELEMENT, bool>()
        {
            {ELEMENT.FIRE, false},
            {ELEMENT.COLD, false},
            {ELEMENT.LIGHTNING, false},
            {ELEMENT.SHADOW, false}
        };
        public Dictionary<ELEMENT, int> ailmentIntensity = new Dictionary<ELEMENT, int>
        {
            {ELEMENT.FIRE, 0},
            {ELEMENT.COLD, 0},
            {ELEMENT.LIGHTNING, 0},
            {ELEMENT.SHADOW, 0}
        };*/

        public int allres = 0;
        public Dictionary<ELEMENT, int> eleres = new Dictionary<ELEMENT, int>
        {
            {ELEMENT.FIRE, 0},
            {ELEMENT.COLD, 0},
            {ELEMENT.LIGHTNING, 0},
            {ELEMENT.SHADOW, 0}
        };
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

        public int bonusLife = 0;
        public int bonusMana = 0;

        public int critBoost = 0;
        public float critMultiplier = 1f;
        public int evasion = 2;
        public int accuracy = 0;
        public float damageBoost = 1f;
        public float damageMultiplier
        {
            get
            {
                return (1f + TotalStats(STAT.POTENCY) * 0.05f + Math.Min(0.09f, TotalStats(STAT.POTENCY) * 0.06f)) * damageBoost;
            }
        }
        public float hitChance
        {
            get
            {
                float diff = 7f + level / 40f;
                return 1f - diff * (1f - 0.85f) / (accuracy + diff);
            }
        }
        public float critHitChance
        {
            get
            {
                float diff = 4f + level / 12f;
                return diff * (1f - 0.8f) / (accuracy + diff);
            }
        }
        public float accuracyCounter = 0.5f;
        public float critAccuracyCounter = 0.5f;
        public int evasionCounter = 50;
        public int bigHitCounter = 50;
        public int critCounter = 50;
        public int bigCritCounter = 50;

        public float lifeLeech = 0f;
        private int leechCooldown = 0;

        public float lifeRegen = 1f;
        private float regenTimer = 0;
        public float lifeDegen = 0f;
        private float degenTimer = 0;

        public int mana = 0;
        public float manaRegen = 0f;
        private float manaRegenTimer = 0;
        public bool canHealMana = true;

        private int levelAnimation = 0;

        private bool initialized = false;
        public StatusBar statusBar;
        public InventoryGUI inventoryGUI;
        public LevelGUI levelGUI;
        public AnvilGUI anvilGUI;
        public AbilitiesGUI abilitiesGUI;
        public SpellcraftingGUI spellcraftingGUI;

        public ProceduralSpell[] abilities = new ProceduralSpell[4];
        public ProceduralSpell selectedAbility = null;
        public List<ProceduralMinion> minions = new List<ProceduralMinion>();
        public List<SpellEffect> spellEffects = new List<SpellEffect>();
        public List<ProceduralSpellProj> circlingProtection = new List<ProceduralSpellProj>();
        public List<Trail> trails = new List<Trail>();
        public int permanence = 0;
        public int transcendence = 0;
        public Item lastSelectedWeapon = null;

        public Item[][] inventories = new Item[3][]
        {
            new Item[40],
            new Item[40],
            new Item[40]
        };
        public int activeInvPage = 0;
        public bool statPage = true;

        public int TotalStats(STAT stat)
        {
            if (rituals[RITUAL.DEMON_PACT] && stat == STAT.POTENCY)
                return baseStats[stat] + tempStats[stat] + baseStats[STAT.RESILIENCE];

            if (rituals[RITUAL.DEMON_PACT] && stat == STAT.RESILIENCE)
                return tempStats[stat];

            return baseStats[stat] + tempStats[stat];
        }

        public PlayerCharacter() : base()
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
                    abilities[i].glyphs[j].SetDefaults();
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
                    inventories[i][j].SetDefaults();
                }
            }
        }

        public override void SetupStartInventory(IList<Item> items)
        {
            Random rand = new Random();
            switch (rand.Next(7))
            {
                default:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TinBroadsword : ItemID.CopperBroadsword);
                    break;
                case 1:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.Spear : ItemID.Trident);
                    break;
                case 2:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.EnchantedBoomerang : ItemID.WoodenBoomerang);
                    break;
                case 3:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TopazStaff : ItemID.AmethystStaff);
                    break;
                case 4:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.TinBow : ItemID.CopperBow);
                    Item arrows = new Item();
                    arrows.SetDefaults(rand.Next(2) == 0 ? ItemID.FlamingArrow : ItemID.WoodenArrow);
                    arrows.stack = rand.Next(2) == 0 ? 150 : 200;
                    items.Add(arrows);
                    break;
                case 5:
                    items[0].SetDefaults(ItemID.Shuriken);
                    items[0].stack = rand.Next(2) == 0 ? 150 : 100;
                    Item knives = new Item();
                    knives.SetDefaults(rand.Next(2) == 0 ? ItemID.PoisonedKnife : ItemID.ThrowingKnife);
                    knives.stack = 50;
                    items.Add(knives);
                    break;
                case 6:
                    items[0].SetDefaults(rand.Next(2) == 0 ? ItemID.ChainKnife : ItemID.WoodYoyo);
                    break;
            }
            items[1].SetDefaults(rand.Next(3) == 0 ? ItemID.TinPickaxe : rand.Next(2) == 0 ? ItemID.CactusPickaxe : ItemID.CopperPickaxe);
            items[1].GetGlobalItem<kItem>().Initialize(items[1], true);
            items[2].SetDefaults(rand.Next(2) == 0 ? ItemID.TinAxe : ItemID.CopperAxe);
            items[2].GetGlobalItem<kItem>().Initialize(items[2], true);
            
            Item star = new Item();
            star.SetDefaults(mod.ItemType<Star_Blue>());
            Item cross = new Item();
            switch (rand.Next(4))
            {
                default:
                    cross.SetDefaults(mod.ItemType<Cross_Red>());
                    break;
                case 1:
                    cross.SetDefaults(mod.ItemType<Cross_Orange>());
                    break;
                case 2:
                    cross.SetDefaults(mod.ItemType<Cross_Yellow>());
                    break;
                case 3:
                    cross.SetDefaults(mod.ItemType<Cross_Green>());
                    break;
            }
            ((Glyph)cross.modItem).Randomize();
            Item moon = new Item();
            switch (rand.Next(4))
            {
                default:
                    moon.SetDefaults(mod.ItemType<Moon_Yellow>());
                    break;
                case 1:
                    moon.SetDefaults(mod.ItemType<Moon_Green>());
                    break;
                case 2:
                    moon.SetDefaults(mod.ItemType<Moon_Blue>());
                    break;
                case 3:
                    moon.SetDefaults(mod.ItemType<Moon_Violet>());
                    break;
            }
            ((Glyph)moon.modItem).Randomize();
            items.Add(star);
            items.Add(cross);
            items.Add(moon);
        }

        public double itemRotation = 0;

        /*public override void ModifyDrawLayers(List<PlayerLayer> layers)
        {
            for (int i = 0; i < layers.Count; i += 1)
            {
                if (layers[i].Name.Contains("Held"))
                {
                    layers.Insert(i + 2, new PlayerLayer("kRPG", "ProceduralItem", (drawinfo) =>
                    {
                        if (player.itemAnimation > 0)
                        {
                            if (player.HeldItem.type == mod.GetItem("ProceduralStaff").item.type)
                            {
                                if (Main.gameMenu) return;

                                ProceduralStaff staff = (ProceduralStaff)player.HeldItem.modItem;

                                Vector2 pos = player.Center - Main.screenPosition;
                                staff.DrawHeld(drawinfo, Lighting.GetColor((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f)), (float)player.itemRotation + (float)API.Tau * player.direction / 8, staff.item.scale, pos);
                            }
                            else if (player.HeldItem.type == mod.GetItem("ProceduralSword").item.type)
                            {
                                if (Main.gameMenu) return;

                                ProceduralSword sword = (ProceduralSword)player.HeldItem.modItem;

                                if (sword.spear) return;

                                Vector2 pos = player.Center - Main.screenPosition;
                                sword.DrawHeld(drawinfo, Lighting.GetColor((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f)), (float)player.itemRotation + (float)API.Tau, sword.item.scale, pos);
                            }
                        }
                    }));
                }
            }
        }*/

        public void LevelUP()
        {
            this.level += 1;
            if (!Main.gameMenu) GFX.sfx_levelUp.Play(0.6f*Main.soundVolume, 0f, 0f);
            levelAnimation = 0;
            Main.NewText("Congratulations! You are now level " + level.ToString(), 255, 223, 63);
        }

        public override void ResetEffects()
        {
            foreach (STAT stat in Enum.GetValues(typeof(STAT)))
                tempStats[stat] = 0;
            evasion = 2;
            accuracy = 0;
            damageBoost = 1f;
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
                //if (!hasAilment[element]) ailmentIntensity[element] = 0;
                //hasAilment[element] = false;
            }
        }

        /*public override void DrawEffects(PlayerDrawInfo drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (player.statLife < 1) return;
            if (hasAilment[ELEMENT.FIRE])
            {
                if (Main.rand.Next(2) == 0)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Fire, player.velocity.X * 0.4f, player.velocity.Y * 0.4f, 100, default(Color), 3.5f);
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
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, mod.GetDust<Ice>().Type, player.velocity.X, player.velocity.Y, 100, Color.White, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
                Lighting.AddLight(player.position, 0f, 0.4f, 1f);
            }

            if (hasAilment[ELEMENT.LIGHTNING])
            {
                if (Main.rand.Next(2) == 0)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Electric, player.velocity.X, player.velocity.Y, 100, default(Color), 0.5f);
                    Main.dust[dust].noGravity = true;
                }
                Lighting.AddLight(player.position, 0.5f, 0.5f, 0.5f);
                fullBright = true;
            }

            if (hasAilment[ELEMENT.SHADOW])
            {
                if (Main.rand.Next(3) < 2)
                {
                    int dust = Dust.NewDust(player.position - new Vector2(2f, 2f), player.width + 4, player.height + 4, DustID.Shadowflame, player.velocity.X, player.velocity.Y, 100, default(Color), 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (Main.netMode == 2) return;
            SpriteBatch spriteBatch = Main.spriteBatch;

            foreach (Trail trail in trails.ToArray())
                trail.Draw(spriteBatch, player);

            if (levelAnimation < 60)
            {
                if (levelAnimation < 24)
                {
                    fullBright = true;
                    Lighting.AddLight(player.position, 0.9f, 0.9f, 0.9f);
                }
                else Lighting.AddLight(player.position, 0.4f, 0.4f, 0.4f);
                spriteBatch.Draw(GFX.levelUp, player.Bottom - new Vector2(48, 108) - Main.screenPosition, new Rectangle(0, (int)(levelAnimation / 3) * 96, 96, 96), Color.White);
                levelAnimation += 1;
            }
        }*/

        public override void PostUpdateEquips()
        {
            if (!initialized)
            {
                InitializeGUI();
                initialized = true;
            }

            UpdateStats();
            if (lifeRegen > 0 && !player.bleed && !player.onFire && !player.poisoned && !player.onFire2 && !player.venom && !player.onFrostBurn) regenTimer += 1f;
            if (regenTimer > 60f / lifeRegen)
            {
                player.statLife = Math.Min(player.statLife + (int)(regenTimer / (60f / lifeRegen)), player.statLifeMax2);
                regenTimer = regenTimer % (60f / lifeRegen);
            }
            if (lifeDegen > 0) degenTimer += 1f;
            /*if (degenTimer >= 20f && hasAilment[ELEMENT.FIRE])
            {
                int amount = (int)Math.Round(lifeDegen / 3, 1);
                player.statLife = player.statLife - amount;
                CombatText.NewText(new Rectangle((int)player.position.X, (int)player.position.Y, player.width, player.height), new Color(255, 95, 31), amount);
                degenTimer = 0;
                if (player.statLife <= 0) player.KillMe(PlayerDeathReason.ByCustomReason(player.name + " burned to death."), amount, 0);
            }*/
            manaRegenTimer += 1f;

            if (Main.chatRelease && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest)
                for (int i = 0; i < abilities.Length; i += 1)
                    if (abilities[i].CompleteSkill())
                    {
                        bool useable = true;
                        foreach (Item item in abilities[i].glyphs)
                        {
                            Glyph glyph = (Glyph)item.modItem;
                            if (!glyph.CanUse()) useable = false;
                        }

                        if (Main.keyState.IsKeyDown(abilities[i].key) && abilities[i].remaining == 0 && useable && player.statMana >= abilities[i].ManaCost(this))
                        {
                            if (Main.netMode == 1)
                            {
                                ModPacket packet = mod.GetPacket();
                                packet.Write((byte)Message.CastSpell);
                                packet.Write(player.whoAmI);
                                packet.Write(i);
                                packet.Write(Main.MouseWorld.X);
                                packet.Write(Main.MouseWorld.Y);
                                packet.Send();
                            }
                            if (Main.netMode == 0)
                                abilities[i].UseAbility(player, Main.MouseWorld);
                            player.statMana -= abilities[i].ManaCost(this);
                        }
                    }

            for (int i = 0; i < spellEffects.Count; i += 1)
                spellEffects[i].Update(this);

            if (Main.mapStyle == 0 && kConfig.configLocal.clientside.arpgMinimap) Main.mapStyle += 1;
        }

        public override void PostItemCheck()
        {
            Item item = player.inventory[player.selectedItem];
            //if (item.type != mod.ItemType<ProceduralStaff>() || item.shoot > 0)
                return;
            player.releaseUseItem = true;
            if (player.itemAnimation == 1 && item.stack > 0)
            {
                if (player.whoAmI != Main.myPlayer && player.controlUseItem)
                {
                    player.itemAnimation = (int)(item.useAnimation / PlayerHooks.TotalMeleeSpeedMultiplier(player, item));
                    player.itemAnimationMax = player.itemAnimation;
                    player.reuseDelay = (int)(item.reuseDelay / PlayerHooks.TotalUseTimeMultiplier(player, item));
                    if (item.UseSound != null)
                    {
                        Main.PlaySound(item.UseSound, player.Center);
                    }
                }
                else
                {
                    player.itemAnimation = 0;
                }
            }
            if (player.itemTime < 2)
            {
                Vector2 pos = player.RotatedRelativePoint(player.MountedCenter, true);
                Vector2 relativeMousePos = Main.MouseWorld - pos;
                itemRotation = Math.Atan2((relativeMousePos.Y * player.direction), relativeMousePos.X * player.direction) - player.fullRotation;
                NetMessage.SendData(13, -1, -1, null, player.whoAmI, 0f, 0f, 0f, 0, 0, 0);
                NetMessage.SendData(41, -1, -1, null, player.whoAmI, 0f, 0f, 0f, 0, 0, 0);
            }
            float scaleFactor = 6f;
            if (player.itemAnimation > 0)
                player.itemRotation = (float)itemRotation;
            player.itemLocation = player.MountedCenter;
            player.itemLocation += player.itemRotation.ToRotationVector2() * scaleFactor * (float)player.direction;
        }

        public override void PostUpdate()
        {
            /*Item item = player.inventory[player.selectedItem];
            if (item.damage > 0 && (item.melee || !item.noMelee || item.modItem is ProceduralSword))
                lastSelectedWeapon = item;

            if (item.modItem is ProceduralSword)
                if (Main.itemTexture[item.type] != ((ProceduralSword)item.modItem).texture)
                    Main.itemTexture[item.type] = ((ProceduralSword)item.modItem).texture;

            if (item.modItem is ProceduralStaff)
                if (Main.itemTexture[mod.ItemType<ProceduralStaff>()] != ((ProceduralStaff)item.modItem).texture)
                    Main.itemTexture[mod.ItemType<ProceduralStaff>()] = ((ProceduralStaff)item.modItem).texture;*/

            //for (int i = 0; i < 40; i += 1)
            //    inventories[activeInvPage][i] = player.inventory[i + 10];
            if (Main.netMode != 2) inventoryGUI.guiActive = Main.playerInventory;

            API.FindRecipes();
        }

        public void OpenInventoryPage(int page)
        {
            for (int i = 0; i < 40; i += 1)
                player.inventory[i + 10] = inventories[page][i];
            activeInvPage = page;
            statPage = false;
            Recipe.FindRecipes();
            for (int i = 0; i < 50; i += 1)
                if (player.inventory[i].type == 71 || player.inventory[i].type == 72 || player.inventory[i].type == 73 || player.inventory[i].type == 74)
                    player.DoCoins(i);
        }

        public override bool PreHurt(bool pvp, bool quiet, ref int damage, ref int hitDirection, ref bool crit, ref bool customDamage, ref bool playSound, ref bool genGore, ref Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            bool enemyCrit = Main.rand.Next(5) == 0 && Main.netMode == 0;
            int max = 80;
            int diff = 52;

            if (TotalStats(STAT.QUICKNESS) > 0 && !rituals[RITUAL.STONE_ASPECT])
            {
                if (damage < (level + 10) * 5)
                {
                    evasionCounter += 100 - max + (max * diff) / (diff + evasion);
                    if (evasionCounter >= 100)
                    {
                        evasionCounter -= 100;
                        if (enemyCrit)
                        {
                            critCounter += 100 - max + (max * diff) / (diff + evasion);
                            if (critCounter >= 100)
                                critCounter -= 100;
                            else
                                enemyCrit = false;

                            if (enemyCrit) damage = (int)(damage * 1.65);
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
                    bigHitCounter += (100 - max) + (max * diff) / (diff + evasion);
                    if (bigHitCounter >= 100)
                    {
                        bigHitCounter -= 100;
                        if (enemyCrit)
                        {
                            bigCritCounter += (100 - max) + (max * diff) / (diff + evasion + TotalStats(STAT.WITS) * 5);
                            if (bigCritCounter >= 100)
                                bigCritCounter -= 100;
                            else
                                enemyCrit = false;

                            if (enemyCrit) damage = (int)(damage * 1.3);
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
                int i = (int)Math.Round(damage * 0.25);
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

        public void UpdateStats()
        {
            player.statLifeMax2 += 90 + TotalStats(STAT.RESILIENCE) * 15 + level * 10 + bonusLife - player.statLifeMax;
            player.statManaMax2 += 14 + (level - 1) * 2 + bonusMana + TotalStats(STAT.WITS) * 7 - player.statManaMax;
            player.statDefense += TotalStats(STAT.RESILIENCE);
            allres += TotalStats(STAT.WITS);
            evasion += TotalStats(STAT.QUICKNESS);
            accuracy += TotalStats(STAT.WITS);
            if (rituals[RITUAL.STONE_ASPECT]) player.statDefense += TotalStats(STAT.QUICKNESS);
            lifeRegen += TotalStats(STAT.RESILIENCE) * 0.3f + TotalStats(STAT.WITS) * 0.2f;
            /*if (hasAilment[ELEMENT.FIRE])
                lifeDegen = ailmentIntensity[ELEMENT.FIRE] / 2;*/
            manaRegen = player.statManaMax2 * 0.06f + TotalStats(STAT.WITS) * 0.6f;

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
                    mana += (int)(player.statLife - player.statLifeMax2 * 0.4);
                    player.statLife = (int)(player.statLifeMax2 * 0.4);
                }
            }
            if (player.statMana == player.statManaMax2 && mana == player.statMana - 1)
                mana = player.statMana;
            else player.statMana = mana;
            if (manaRegenTimer > 60f / manaRegen)
            {
                mana = Math.Min(mana + (int)(manaRegenTimer / (60f / manaRegen)), player.statManaMax2);
                manaRegenTimer = manaRegenTimer % (60f / manaRegen);
            }
            player.statMana = mana;

            critMultiplier += TotalStats(STAT.POTENCY) * 0.06f;
            lifeLeech += TotalStats(STAT.POTENCY) * 0.004f;
            lifeLeech += Math.Min(0.006f, TotalStats(STAT.POTENCY) * 0.002f);

            player.meleeDamage *= damageMultiplier;
            player.rangedDamage *= damageMultiplier;
            player.magicDamage *= damageMultiplier;
            player.minionDamage *= damageMultiplier;
            player.thrownDamage *= damageMultiplier;

            player.moveSpeed *= 1f + Math.Min(1.5f, TotalStats(STAT.QUICKNESS) * 0.03f + Math.Min(level * 0.01f, 0.6f));
            player.meleeSpeed *= 1f + TotalStats(STAT.QUICKNESS) * 0.01f;
            player.jumpSpeedBoost += Math.Min(5f, TotalStats(STAT.QUICKNESS) * 0.2f + Math.Min(level * 0.1f, 2f));

            critBoost += TotalStats(STAT.QUICKNESS);
            player.magicCrit += critBoost;
            player.meleeCrit += critBoost;
            player.rangedCrit += critBoost;
            player.thrownCrit += critBoost;
        }

        /*public override void ModifyHitByNPC(NPC npc, ref int damage, ref bool crit)
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
                dict[element] = proj.GetGlobalProjectile<kProjectile>().elementalDamage[element];
            ModifyDamageTakenFromNPC(ref damage, ref crit, dict);
        }

        public void ModifyDamageTakenFromNPC(ref int damage, ref bool crit, Dictionary<ELEMENT, int> eleDmg)
        {
            damage = (int)(0.38 * Math.Pow(damage, 1.4));
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                eleDmg[element] = (int)(0.38 * Math.Pow(eleDmg[element], 1.4));
            if (!Main.expertMode)
            {
                damage = (int)(damage * 1.4);
                foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                    eleDmg[element] = (int)(eleDmg[element] * 1.4);
            }
            bool bossfight = false;
            foreach (NPC n in Main.npc)
                if (n.active)
                    if (n.boss) bossfight = true;
            int elecount = 0;
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
                if (eleDmg[element] > 0) elecount += 1;
            if (elecount > 0) damage = (int)Math.Round(damage * (kNPC.ELE_DMG_MODIFIER + 1) / 2);
            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
            {
                damage -= Math.Min(resistance[element], eleDmg[element] * 3 / 5);
                if (Main.rand.Next(player.statLifeMax2 + resistance[element] * 20) < 15 + eleDmg[element] * (bossfight ? 2 : 8) && Main.netMode != 2)
                {
                    if (eleDmg[element] > 0)
                    {
                        Type t = ailments[element];
                        ModBuff buff;
                        if (ailments[element] == typeof(Fire))
                            buff = mod.GetBuff<Fire>();
                        else if (ailments[element] == typeof(Cold))
                            buff = mod.GetBuff<Cold>();
                        else if (ailments[element] == typeof(Lightning))
                            buff = mod.GetBuff<Lightning>();
                        else
                            buff = mod.GetBuff<Shadow>();
                        player.AddBuff(buff.Type, bossfight ? 90 : 210);
                        ailmentIntensity[element] = Main.expertMode ? eleDmg[element] * 2 / 3 : eleDmg[element];
                        hasAilment[element] = true;
                    }
                }
            }
            if (Main.rand.Next(player.statLifeMax2) < damage * 2)
                player.AddBuff(mod.BuffType<Physical>(), 15 + Math.Min(15, damage * 30 / player.statLifeMax2));
            if (hasAilment[ELEMENT.LIGHTNING])
                damage += 1 + ailmentIntensity[ELEMENT.LIGHTNING];
        }*/

        public override void ModifyHitNPC(Item item, NPC target, ref int damage, ref float knockback, ref bool crit)
        {
            ModifyDamage(ref damage, ref crit, target, item);
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref int damage, ref float knockback, ref bool crit, ref int hitDirection)
        {
            //if (proj.modProjectile is ProceduralSpellProj)
            //    ModifyDamage(ref damage, ref crit, target, null, proj);
            //else
                ModifyDamage(ref damage, ref crit, target, null, proj);
        }

        private void ModifyDamage(ref int damage, ref bool crit, NPC target, Item item = null, Projectile proj = null)
        {
            if (rituals[RITUAL.WARRIOR_OATH])
            {
                crit = false;
                float damageBoost = 1f + TotalStats(STAT.RESILIENCE) * 0.04f;
                damageBoost += Math.Min(0.1f, TotalStats(STAT.RESILIENCE) * 0.02f);
                damage = (int)(damage * damageBoost);
            }

            Dictionary<ELEMENT, int> eleDmg = new Dictionary<ELEMENT, int>();
            /*
            if (item != null)
            {
                kItem ki = item.GetGlobalItem<kItem>(mod);
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
                crit = Main.rand.Next(500) < 50 + victim.ailmentIntensity[ELEMENT.COLD];*/

            if (crit)
            {
                float mult = 1;
                if (item != null)
                    mult = DamageMultiplier(null, item.melee, item.ranged, item.magic, item.thrown, item.summon);
                else if (proj != null)
                    mult = DamageMultiplier(null, proj.melee, proj.ranged, proj.magic, proj.thrown, proj.minion);
                damage = (int)(damage / mult * (mult + critMultiplier));
                if (rituals[RITUAL.ELDRITCH_FURY])
                {
                    int i = damage;
                    int c = target.boss ? 7 : 2;
                    damage += Math.Min(mana * c, i);
                    mana = Math.Max(0, mana - i / c);
                }
            }

            if (item == null && proj == null) return;
            
            /*if (victim.hasAilment[ELEMENT.LIGHTNING])
                damage += 1 + victim.ailmentIntensity[ELEMENT.LIGHTNING];

            foreach (ELEMENT element in Enum.GetValues(typeof(ELEMENT)))
            {
                if (Main.rand.Next(target.boss ? 500 : 200) < 30 + eleDmg[element])
                {
                    if (eleDmg[element] > 0)
                    {
                        Type t = ailments[element];
                        ModBuff buff;
                        if (ailments[element] == typeof(Fire))
                            buff = mod.GetBuff<Fire>();
                        else if (ailments[element] == typeof(Cold))
                            buff = mod.GetBuff<Cold>();
                        else if (ailments[element] == typeof(Lightning))
                            buff = mod.GetBuff<Lightning>();
                        else
                            buff = mod.GetBuff<Shadow>();
                        target.AddBuff(buff.Type, target.boss ? 30 + Math.Min(eleDmg[element], 30) * 3 : 120 + Math.Min(eleDmg[element], 15) * 12);
                        victim.ailmentIntensity[element] = target.boss ? eleDmg[element] / 2 : eleDmg[element];
                        victim.hasAilment[element] = true;
                    }
                }
            }*/
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

        public void AddXP(int xp)
        {
            if (Main.gameMenu) return;
            if (xp == 0) return;
            this.xp += xp;

            Check:
            if (this.xp >= ExperienceToLevel())
            {
                this.xp -= ExperienceToLevel();
                LevelUP();
                goto Check;
            }
            else
                CombatText.NewText(player.getRect(), new Color(127, 159, 255), xp + " XP");
        }

        public int ExperienceToLevel()
        {
            if (level < 5)
                return 80 + level * 20;
            else if (level < 10)
                return level * 40;
            else
                return (int)(280 * Math.Pow(1.09, level - 5) + 3 * level);
        }

        public override void OnHitNPC(Item item, NPC target, int damage, float knockback, bool crit)
        {
            LeechLife(item, damage);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, int damage, float knockback, bool crit)
        {
            Item item = player.inventory[player.selectedItem];
            LeechLife(item, damage);
            /*if (item.type == mod.ItemType<ProceduralStaff>())
            {
                ProceduralStaff staff = (ProceduralStaff)item.modItem;
                bool proceed = false;
                if (proj.type == item.shoot)
                    proceed = true;
                else if (proj.type == mod.ProjectileType<ProceduralSpellProj>())
                    proceed = ((ProceduralSpellProj)proj.modProjectile).source == null;
                if (proceed && staff.ornament != null)
                    if (staff.ornament.onHit != null) staff.ornament.onHit(player, target, item, damage, crit);

            }
            else if (proj.type == mod.ProjectileType<ProceduralSpear>() && item.type == mod.ItemType<ProceduralSword>())
            {
                ProceduralSword spear = (ProceduralSword)item.modItem;
                if (spear.accent != null)
                    if (spear.accent.onHit != null) spear.accent.onHit(player, target, spear, damage, crit);
            }*/
        }

        private void LeechLife(Item item, int damage)
        {
            if (leechCooldown == 0)
            {
                int leechAmount = Math.Min((int)(damage * lifeLeech), (int)(player.inventory[player.selectedItem].damage / 2 * (1 + lifeLeech)));
                leechAmount = Math.Min(leechAmount, (int)(player.statLifeMax2 * lifeLeech * 0.2));
                if (leechAmount > 1)
                {
                    player.statLife += leechAmount;
                    player.HealEffect((int)(leechAmount));
                    leechCooldown = item.useAnimation * 3;
                }

                else if (lifeLeech > 0f)
                {
                    player.statLife += 1;
                    player.HealEffect(1);
                    leechCooldown = (int)(item.useAnimation * (3 - Math.Min(1.4f, lifeLeech * 10f)));
                }
            }
        }

        public bool UnspentPoints()
        {
            return pointsAllocated < level - 1;
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
                {
                    inventories[i][j] = new Item();
                    inventories[i][j].SetDefaults();
                }
            }

            rituals = new Dictionary<RITUAL, bool>();
            foreach (RITUAL rite in Enum.GetValues(typeof(RITUAL)))
                rituals[rite] = false;

            /*for (int i = 0; i < abilities.Length; i += 1)
            {
                abilities[i] = new ProceduralSpell(mod);
                for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                {
                    abilities[i].glyphs[j] = new Item();
                    abilities[i].glyphs[j].SetDefaults();
                }
            }
            abilities[0].key = Keys.Z;
            abilities[1].key = Keys.X;
            abilities[2].key = Keys.C;
            abilities[3].key = Keys.V;*/
        }

        public void InitializeGUI()
        {
            if (Main.netMode == 2) return;
            BaseGUI.gui_elements.Clear();
            statusBar = new StatusBar(this, mod);
            statusBar.guiActive = true;
            inventoryGUI = new InventoryGUI(this, mod);
            levelGUI = new LevelGUI(this, mod);
            anvilGUI = new AnvilGUI(mod, this);
            /*abilitiesGUI = new AbilitiesGUI(this, mod);
            abilitiesGUI.guiActive = true;
            spellcraftingGUI = new GUI.SpellcraftingGUI(this, mod);*/
        }

        public void CloseGUIs()
        {
            levelGUI.CloseGUI();
            anvilGUI.CloseGUI();
            //spellcraftingGUI.CloseGUI();
        }

        public override void OnEnterWorld(Player player)
        {
            InitializeGUI();
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
                {"life", player.statLife },
                {"permanence", permanence },
                {"transcendence", transcendence }
            };

            try
            {
                /*for (int i = 0; i < abilities.Length; i += 1)
                {
                    if (abilities[i] == null) return tagCompound;
                    tagCompound.Add("abilities" + i.ToString() + "_key", abilities[i].key.ToString());
                    for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                        if (abilities[i].glyphs[j] != null)
                            tagCompound.Add("ability" + i + j, ItemIO.Save(abilities[i].glyphs[j]));
                }*/
            }
            catch (SystemException e)
            {
                ErrorLogger.Log("@Abilities :: " + e.ToString());
            }

            try
            {
                for (int i = 0; i < inventories.Length; i += 1)
                    for (int j = 0; j < inventories[i].Length; j += 1)
                        tagCompound.Add("item" + i + j, ItemIO.Save(inventories[i][j]));
            }
            catch (SystemException e)
            {
                ErrorLogger.Log("@Inventories :: " + e.ToString());
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
                ErrorLogger.Log("@Level&XP :: " + e.ToString());
            }
            try
            {
                foreach (STAT stat in Enum.GetValues(typeof(STAT)))
                    baseStats[stat] = tag.GetInt("base" + stat.ToString());
                foreach (RITUAL rite in Enum.GetValues(typeof(RITUAL)))
                    rituals[rite] = tag.GetBool("RITUAL_" + rite.ToString());
            }
            catch (SystemException e)
            {
                ErrorLogger.Log("@Stats&Rituals :: " + e.ToString());
            }
            try
            {
                /*abilities = new ProceduralSpell[4];
                for (int i = 0; i < abilities.Length; i += 1)
                {
                    abilities[i] = new ProceduralSpell(mod);
                    abilities[i].key = (Keys)Enum.Parse(typeof(Keys), tag.GetString("abilities" + i.ToString() + "_key"));
                    for (int j = 0; j < abilities[i].glyphs.Length; j += 1)
                    {
                        if (tag.ContainsKey("ability" + i + j))
                            abilities[i].glyphs[j] = ItemIO.Load(tag.GetCompound("ability" + i + j));
                    }
                }*/
            }
            catch (SystemException e)
            {
                ErrorLogger.Log("@Abilities :: " + e.ToString());
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
                ErrorLogger.Log("@Inventory :: " + e.ToString());
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
                ErrorLogger.Log("@Miscellaneous :: " + e.ToString());
            }
        }

        #endregion

        /*public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            foreach (Projectile projectile in Main.projectile)
                if (projectile.modProjectile is ProceduralSpear || projectile.modProjectile is ProceduralMinion)
                    projectile.Kill();
            foreach (ProceduralSpellProj spell in circlingProtection)
                spell.projectile.Kill();
            circlingProtection.Clear();
        }*/
    }
}
