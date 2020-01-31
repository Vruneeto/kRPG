﻿using System;
using kRPG.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace kRPG.Items.Glyphs
{
    public class Cross_Green : Cross
    {
        public override float BaseManaModifier()
        {
            return 0.9f;
        }

        public override Action<ProceduralSpellProj> GetAiAction()
        {
            return delegate(ProceduralSpellProj spell)
            {
                if (spell.projectile.velocity.X < 0 && spell.basePosition == Vector2.Zero) spell.projectile.spriteDirection = -1;
                Vector2 v = spell.basePosition != Vector2.Zero ? spell.basePosition : spell.origin;
                spell.projectile.rotation = (spell.projectile.Center - v).ToRotation() - (float) API.Tau / 4f;
            };
        }

        public override Action<ProceduralSpellProj> GetInitAction()
        {
            return delegate(ProceduralSpellProj spell)
            {
                spell.texture = Main.itemTexture[ItemID.WoodenArrow];
                spell.projectile.width = spell.texture.Width;
                spell.projectile.height = spell.texture.Height;
                spell.projectile.ranged = true;
                spell.drawTrail = true;
                spell.alpha = 1f;
                spell.lighted = true;
                spell.projectile.scale = spell.minion ? 1f : 1.5f;
            };
        }

        public override Action<ProceduralSpellProj> GetKillAction()
        {
            return delegate(ProceduralSpellProj spell)
            {
                for (int k = 0; k < 5; k++)
                    Dust.NewDust(spell.projectile.position + spell.projectile.velocity, spell.projectile.width, spell.projectile.height, DustID.Stone,
                        spell.projectile.oldVelocity.X * 0.5f, spell.projectile.oldVelocity.Y * 0.5f);
            };
        }

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Green Cross Glyph");
            Tooltip.SetDefault("Conjures giant arrows that deal ranged damage");
        }
    }
}