﻿using RandomBuff;
using RandomBuff.Core.Game;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RandomBuffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RWCustom;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using BuiltinBuffs.Duality;
using BuiltinBuffs.Negative;
using System.Globalization;
using MoreSlugcats;

namespace BuiltinBuffs.Duality
{
    internal class WaterWorldBuff : Buff<WaterWorldBuff, WaterDancerBuffData>
    {
        public override BuffID ID => WaterDancerBuffEntry.WaterWorld;
        
        public WaterWorldBuff()
        {
        }
    }

    internal class WaterDancerBuffData : CountableBuffData
    {
        public override BuffID ID => WaterDancerBuffEntry.WaterWorld;
        public override int MaxCycleCount => 5;
    }

    internal class WaterDancerBuffEntry : IBuffEntry
    {
        public static BuffID WaterWorld = new BuffID("WaterWorld", true);

        public void OnEnable()
        {
            BuffRegister.RegisterBuff<WaterWorldBuff, WaterDancerBuffData, WaterDancerBuffEntry>(WaterWorld);
        }
        
        public static void HookOn()
        {
            On.Room.Update += Room_Update;
        }

        public static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            if (!self.abstractRoom.shelter && !self.abstractRoom.gate)
            {
                if (WaterWorldBuff.Instance.GetTemporaryBuffPool().allBuffIDs.Contains(BuiltinBuffs.Negative.UpsideDownBuffEntry.UpsideDownID))
                {
                    //本末倒置使每个房间变成上半部分被淹没
                    if (self.roomSettings.GetEffect(MoreSlugcatsEnums.RoomEffectType.InvertedWater) == null)
                        self.roomSettings.effects.Add(new RoomSettings.RoomEffect(MoreSlugcatsEnums.RoomEffectType.InvertedWater, 1f, false));
                    else if (self.roomSettings.GetEffect(MoreSlugcatsEnums.RoomEffectType.InvertedWater).amount < 1f)
                        self.roomSettings.GetEffect(MoreSlugcatsEnums.RoomEffectType.InvertedWater).amount = 1f;
                    self.waterInverted = true;
                }
                if (self.waterObject == null)
                    self.AddWater();
                if(self.waterObject.fWaterLevel < self.PixelHeight / 2f)
                {
                    self.defaultWaterLevel = Mathf.RoundToInt(self.Height / 2f);
                    self.waterObject.fWaterLevel = self.PixelHeight / 2f;
                }
            }
        }
    }
}
