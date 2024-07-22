﻿using Mono.Cecil.Cil;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RandomBuffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Expedition;
using JetBrains.Annotations;
using RandomBuff.Core.SaveData;
using MonoMod.Utils;
using RWCustom;
using UnityEngine;
using System.Runtime.InteropServices.ComTypes;

namespace BuiltinBuffs.Expeditions
{
    public class ExpeditionExtend : IBuffEntry
    {

        public void OnEnable()
        {
            On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        }

        private static bool isLoaded = false;

        private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            orig(self);
            try
            {
                if (!isLoaded)
                {
                    ExpeditionProgression.SetupPerkGroups();
                    ExpeditionProgression.SetupBurdenGroups();

                    InitExpeditionType();
                    RegisterExpeditionType();
                    ExpeditionHooks.OnModsInit();
                    isLoaded = true;
                }
            }
            catch (Exception e)
            {
                BuffUtils.LogException("BuffExtend", e);
            }

        }

        public static bool IsUseless(string str)
        {
            return UseLess.Contains(str);
        }

        public static bool OnlyEnglish(string str)
        {
            return str.ToLower().All(i =>
                (i  >= 'a' && i <= 'z') || (i >= '0' && i <= '9') || i == '.' || i == ',');
        }

        private static readonly HashSet<string> UseLess = new HashSet<string>()
        {
            "unl-lantern",
            "unl-bomb",
            "unl-vulture",
            "unl-electric",
            "unl-sing",
            "unl-gun",
            "bur-doomed"
        };

        private static void InitExpeditionType()
        {
            foreach (var group in ExpeditionProgression.perkGroups)
            {
                foreach (var item in group.Value)
                {
                    if (IsUseless(item)) continue;
                    var re = BuffBuilder.GenerateBuffType("BuffExtend", item,
                        true, (il) => BuildILBuffCtor(il, item));
                    re.buffType.DefineMethodOverride("Destroy", typeof(void), Type.EmptyTypes,
                        Mono.Cecil.MethodAttributes.Public, (il) => BuildILDestroy(il, item));
                    //BuffUtils.Log("BuffExtend", $"Build expedition buff {group.Key}:{item}");
                }
            }

            foreach (var group in ExpeditionProgression.burdenGroups)
            {
                foreach (var item in group.Value)
                {
                    if (IsUseless(item)) continue;
                    var re = BuffBuilder.GenerateBuffType("BuffExtend", item,
                   true, (il) => BuildILBuffCtor(il, item));
                    re.buffType.DefineMethodOverride("Destroy", typeof(void), Type.EmptyTypes,
                        Mono.Cecil.MethodAttributes.Public, (il) => BuildILDestroy(il, item));
                    //BuffUtils.Log("BuffExtend", $"Build expedition buff {group.Key}:{item}");
                }
            }
        }

        private static void SetProperty(BuffStaticData data, string name, object value)
        {
            typeof(BuffStaticData).GetProperty(name).SetMethod.Invoke(data, new[] { value });
        }

        private static void RegisterExpeditionType()
        {
            var ass = BuffBuilder.FinishGenerate("BuffExtend");
            var ctor = typeof(BuffStaticData).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)[0];
            Futile.atlasManager.LoadImage("buffassets/cardinfos/expedition/expeditionPositive");
            Futile.atlasManager.LoadImage("buffassets/cardinfos/expedition/expeditionNegative");

            foreach (var group in ExpeditionProgression.perkGroups)
            {
                foreach (var item in group.Value)
                {
                    if (IsUseless(item)) continue;
                    var staticData = (BuffStaticData)ctor.Invoke(Array.Empty<object>());
                    SetProperty(staticData, "BuffID", new BuffID(item));
                    SetProperty(staticData, "BuffType", BuffType.Positive);
                    SetProperty(staticData, "FaceName", "buffassets/cardinfos/expedition/expeditionPositive");
                    SetProperty(staticData, "Color", Custom.hexToColor("2EFFFF"));
                    SetProperty(staticData, "MultiLayerFace", true);
                    SetProperty(staticData, "FaceLayer", 3);
                    SetProperty(staticData, "MaxFaceDepth", 1.0f);
                    SetProperty(staticData, "FaceBackgroundColor", Custom.hexToColor("020B0B"));



                    staticData.CardInfos.Add(Custom.rainWorld.inGameTranslator.currentLanguage, new BuffStaticData.CardInfo()
                    {
                        BuffName = ForceUnlockedAndLoad(ExpeditionProgression.UnlockName, item),
                        Description = ForceUnlockedAndLoad(ExpeditionProgression.UnlockDescription, item),
                    });
                    BuffRegister.InternalRegisterBuff(staticData.BuffID, ass.GetType($"BuffExtend.{item}Buff", true),
                        ass.GetType($"BuffExtend.{item}BuffData"));
                    ExpeditionExtend.RegisterStaticData(staticData);
                }
            }
            foreach (var group in ExpeditionProgression.burdenGroups)
            {
                foreach (var item in group.Value)
                {
                    if (IsUseless(item)) continue;
                    var staticData = (BuffStaticData)ctor.Invoke(Array.Empty<object>());
                    SetProperty(staticData, "BuffID", new BuffID(item));
                    SetProperty(staticData, "BuffType", BuffType.Negative);
                    SetProperty(staticData, "FaceName", "buffassets/cardinfos/expedition/expeditionNegative");
                    SetProperty(staticData, "Color", Custom.hexToColor("FF462E"));
                    SetProperty(staticData, "MultiLayerFace", true);
                    SetProperty(staticData, "FaceLayer", 3);
                    SetProperty(staticData, "MaxFaceDepth", 1.0f);
                    SetProperty(staticData, "FaceBackgroundColor", Custom.hexToColor("0B0302"));
                    var name = ForceUnlockedAndLoad(ExpeditionProgression.BurdenName, item);
                    staticData.CardInfos.Add(OnlyEnglish(name) ? InGameTranslator.LanguageID.English :
                        Custom.rainWorld.inGameTranslator.currentLanguage, new BuffStaticData.CardInfo()
                        {
                            BuffName = name,
                            Description = ForceUnlockedAndLoad(ExpeditionProgression.BurdenManualDescription, item),
                        });
                    BuffRegister.InternalRegisterBuff(staticData.BuffID, ass.GetType($"BuffExtend.{item}Buff", true),
                        ass.GetType($"BuffExtend.{item}BuffData"));
                    ExpeditionExtend.RegisterStaticData(staticData);
                }
            }
        }

        private static string ForceUnlockedAndLoad(Func<string, string> orig, string key)
        {
            if (ExpeditionData.unlockables == null)
                ExpeditionData.unlockables = new List<string>();
            bool contains = ExpeditionData.unlockables.Contains(key);
            if (!contains) ExpeditionData.unlockables.Add(key);
            var re = orig(key);
            if (!contains) ExpeditionData.unlockables.Remove(key);
            return re;
        }

        private static void BuildILDestroy(ILProcessor il, string item)
        {
            il.Emit(OpCodes.Ldsfld, typeof(ExpeditionHooks).GetField(nameof(ExpeditionHooks.activeUnlocks), BindingFlags.Static | BindingFlags.Public));
            il.Emit(OpCodes.Ldstr, item);
            il.Emit(OpCodes.Callvirt, typeof(List<string>).GetMethod(nameof(List<string>.Remove), new[] { typeof(string) }));
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);

        }

        private static void BuildILBuffCtor(ILProcessor il, string item)
        {
            il.Emit(OpCodes.Ldsfld, typeof(ExpeditionHooks).GetField(nameof(ExpeditionHooks.activeUnlocks), BindingFlags.Static | BindingFlags.Public));
            il.Emit(OpCodes.Ldstr, item);
            il.Emit(OpCodes.Callvirt, typeof(ExpeditionExtend).GetMethod(nameof(ExpeditionExtend.AddUnique), new[] { typeof(List<string>), typeof(string) }));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, item);
            il.Emit(OpCodes.Callvirt, typeof(ExpeditionExtend).GetMethod(nameof(ExpeditionExtend.ExpeditionBuffCtor), new[] { typeof(string) }));

            il.Emit(OpCodes.Call, typeof(RuntimeBuff).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First());
            il.Emit(OpCodes.Ret);
        }


        public static void ExpeditionBuffCtor(string id)
        {
            if (BuffCustom.TryGetGame(out var game))
            {
                switch (id)
                {
                    case "unl-backspear":
                        foreach(var ply in game.Players.Select(i => i.realizedCreature as Player))
                            if (ply != null)
                                ply.spearOnBack = new Player.SpearOnBack(ply);
                        break;
                    case "unl-agility":
                        foreach (var ply in game.Players.Select(i => i.realizedCreature as Player))
                            if (ply != null)
                            {
                                ply.slugcatStats.lungsFac = 0.15f;
                                ply.slugcatStats.runspeedFac = 1.75f;
                                ply.slugcatStats.poleClimbSpeedFac = 1.8f;
                                ply.slugcatStats.corridorClimbSpeedFac = 1.6f;
                            }
                        break;
                    case "unl-glow":
                        game.GetStorySession.saveState.theGlow = true;
                        break;
                    case "unl-karma":
                        game.GetStorySession.saveState.deathPersistentSaveData.reinforcedKarma = true;
                        break;
                    case "bur-pursued":
                        if (ExpeditionGame.burdenTrackers.All( i => !(i is ExpeditionGame.PursuedTracker)))
                            ExpeditionGame.burdenTrackers.Add(new ExpeditionGame.PursuedTracker(game));
                        break;
                }
            }
        }


        public static void RegisterStaticData([NotNull] BuffStaticData data)
        {
            var staticDatas =
                (Dictionary<BuffID, BuffStaticData>)typeof(BuffConfigManager).GetField("staticDatas", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (!staticDatas.ContainsKey(data.BuffID))
            {
                BuffUtils.Log("BuffExtend", $"Register Buff:{data.BuffID} static data by Code ");
                staticDatas.Add(data.BuffID, data);
                BuffConfigManager.buffTypeTable[data.BuffType].Add(data.BuffID);
            }
            else
                BuffUtils.Log("BuffExtend", $"already contains BuffID {data.BuffID}");
        }

        public static void AddUnique(List<string> self, string item)
        {
            if (!self.Contains(item))
                self.Add(item);
        }
    }

}
