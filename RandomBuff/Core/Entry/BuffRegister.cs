﻿#define BUFFDEBUG 

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Game;
using RandomBuff.Core.Game.Settings.Conditions;
using RandomBuff.Core.Game.Settings.GachaTemplate;
using RandomBuff.Core.SaveData;
using RandomBuff.Core.SaveData.BuffConfig;
using UnityEngine;
using MethodAttributes = Mono.Cecil.MethodAttributes;


namespace RandomBuff.Core.Entry
{
    

    public enum HookLifeTimeLevel
    {
        InGame,
        UntilQuit
    }



    /// <summary>
    /// 保存Buff的注册信息，负责保存BuffData/Buff的Type信息
    /// 不保存实际的data类型
    /// 不随存档变化刷新
    ///
    /// 外部接口
    /// </summary>
    public static partial class BuffRegister
    {

        /// <summary>
        /// 注册新的Buff，并且包含hook
        /// </summary>
        /// <typeparam name="TBuffType"></typeparam>
        /// <typeparam name="TDataType"></typeparam>
        /// <typeparam name="THookType"></typeparam>
        /// <param name="id"></param>
        public static void RegisterBuff<TBuffType, TDataType, THookType>(BuffID id) where TBuffType : IBuff, new()
            where TDataType : BuffData, new()
        {
            BuffHookWarpper.RegisterHook(id, typeof(THookType));
            RegisterBuff<TBuffType, TDataType>(id);
        }



        /// <summary>
        /// 注册新的buff，不包含hook
        /// </summary>
        /// <typeparam name="TBuffType"></typeparam>
        /// <typeparam name="TDataType"></typeparam>
        /// <param name="id"></param>
        public static void RegisterBuff<TBuffType, TDataType>(BuffID id) where TBuffType : IBuff, new()
            where TDataType : BuffData, new()
        {
            InternalRegisterBuff(id,typeof(TBuffType),typeof(TDataType));
        }

        /// <summary>
        /// 无Buff类型注册Buff
        /// </summary>
        /// <typeparam name="THookType"></typeparam>
        /// <param name="id"></param>
        public static (TypeDefinition buffType, TypeDefinition dataTypeDefinition) RegisterBuff<THookType>(BuffID id)
        {
            BuffHookWarpper.RegisterHook(id, typeof(THookType));
            return RegisterBuff(id);
        }

        /// <summary>
        /// 无Buff类型注册Buff
        /// </summary>
        /// <param name="id"></param>
        public static (TypeDefinition buffType, TypeDefinition dataTypeDefinition) RegisterBuff(BuffID id)
        {
            if (CurrentModId == string.Empty)
            {
                BuffPlugin.LogError("Missing Mod ID!, can't use this out of IBuffEntry.OnEnable");
                return (null, null);
            }

            try
            {
                var re = BuffBuilder.GenerateBuffType(CurrentModId, id.value);
                currentRuntimeBuffName.Add(id.value);
                return re;
            }
            catch (Exception e)
            {
                BuffPlugin.LogException(e,$"Exception in GenerateBuffType:{CurrentModId}:{id}");
            }
            return (null, null);
        }

        internal static void InternalRegisterBuff(BuffID id,Type buffType,Type dataType)
        {
            try
            {
                if (id != Helper.GetUninit<IBuff>(buffType).ID || id != Helper.GetUninit<BuffData>(dataType).ID)
                {
                    BuffPlugin.LogError($"{id}'s Buff or BuffData has unexpected BuffID!");
                    return;
                }
                BuffTypes.Add(id, buffType);
                DataTypes.Add(id, dataType);
            }
            catch (Exception e)
            {
                BuffPlugin.LogException(e,$"Exception when register buff {id}");
            }
      
        }

      
        /// <summary>
        /// 注册新的抽卡模式
        /// </summary>
        /// <typeparam name="TTemplateType"></typeparam>
        /// <param name="id">ID</param>
        /// <param name="banList">ban掉的条件</param>
        public static void RegisterGachaTemplate<TTemplateType>(GachaTemplateID id, params ConditionID[] banList)
            where TTemplateType : GachaTemplate, new()
        {
            try
            {
                if (id != Helper.GetUninit<TTemplateType>().ID)
                {
                    BuffPlugin.LogError($"{id}'s GachaTemplate has unexpected GachaTemplateID!");
                    return;
                }

                TemplateTypes.Add(id,new GachaTemplateType(
                    typeof(TTemplateType),new(banList)));
            }
            catch (Exception e)
            {
                BuffPlugin.LogException(e, $"Exception when register GachaTemplate {id}");
            }
        }

        /// <summary>
        /// 注册新的通关条件
        /// </summary>
        /// <typeparam name="TConditionType"></typeparam>
        /// <param name="id">ID</param>
        /// <param name="displayName">显示类别名称</param>
        /// <param name="canUseMore">是否可以同一局选取多个</param>
        /// <param name="parentId">继承某个条件的Ban情况</param>
        /// <param name="banList">Ban掉特定的游玩模式，请保证对应的GachaTemplate已经注册</param>
        public static void RegisterCondition<TConditionType>(ConditionID id, string displayName,
            ConditionID parentId = null,params GachaTemplateID[] banList)
            where TConditionType : Condition, new()
        {
            try
            {
                if (id != Helper.GetUninit<TConditionType>().ID)
                {
                    BuffPlugin.LogError($"{id}'s Condition has unexpected ConditionID!");
                    return;
                }

                var parent = GetConditionType(parentId);
                if (parent == null && parentId != null)
                {
                    BuffPlugin.LogError($"can't find Condition:{parentId}, When register Condition:{id} parent");
                    return;
                }
                ConditionTypes.Add(id, new ConditionType(id,typeof(TConditionType), GetConditionType(parentId), displayName));
                foreach (var banId in banList)
                {
                    var type = GetTemplateType(banId);
                    if (type == null)
                    {
                        BuffPlugin.LogError($"can't find GachaTemplate:{banId}, When register Condition:{id} ban list");
                        continue;
                    }
                    if (!type.BanConditionIds.Contains(id))
                        type.BanConditionIds.Add(id);
                }
            }
            catch (Exception e)
            {
                BuffPlugin.LogException(e, $"Exception when register condition {id}");
            }
        }
    }

    /// <summary>
    /// 保存Buff的注册信息，负责保存BuffData/Buff的Type信息
    /// 不保存实际的data类型
    /// 不随存档变化刷新
    /// </summary>
    public static partial class BuffRegister
    {
        internal static (BuffID id, Type type) GetDataType(string id) => (new BuffID(id),GetAnyType(new BuffID(id), DataTypes));
        internal static (BuffID id, Type type) GetBuffType(string id) => (new BuffID(id),GetAnyType(new BuffID(id), BuffTypes));

        internal static Type GetDataType(BuffID id) => GetAnyType(id, DataTypes);
        internal static Type GetBuffType(BuffID id) => GetAnyType(id, BuffTypes);

        internal static ConditionType GetConditionType(ConditionID id)
        {
            if(id == null)
                return null;
            return GetAnyType(id, ConditionTypes);
        }
        internal static string GetConditionTypeName(ConditionID id) => GetAnyType(id, ConditionTypes).DisplayName;
        internal static List<ConditionID> GetAllConditionList() => ConditionTypes.Keys.ToList();


        internal static GachaTemplateType GetTemplateType(GachaTemplateID id) => GetAnyType(id, TemplateTypes);


        private static Y GetAnyType<T,Y>(T id, Dictionary<T, Y> dic)
        {
            if (dic.ContainsKey(id))
                return dic[id];
            return default;
        }

        internal static string CurrentModId { get; private set; } = string.Empty;

        private static readonly List<string> currentRuntimeBuffName = new ();

        internal static void InitAllBuffPlugin()
        {
            foreach (var mod in ModManager.ActiveMods)
            {
                string path = mod.path + Path.DirectorySeparatorChar + "buffplugins";
                if (!Directory.Exists(path))
                    continue;
                BuffPlugin.Log($"Find correct path in {CurrentModId = mod.id} to load plugins");
                DirectoryInfo info = new DirectoryInfo(path);
                foreach (var file in info.GetFiles("*.dll"))
                {
                    var assembly = Assembly.LoadFile(file.FullName);
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.GetInterfaces().Contains(typeof(IBuffEntry)))
                        {
                            var obj = type.GetConstructor(Type.EmptyTypes).Invoke(Array.Empty<object>());
                            try
                            {
                                type.GetMethod("OnEnable").Invoke(obj, Array.Empty<object>());
                                BuffPlugin.Log($"Invoke {type.Name}.OnEnable");
                            }
                            catch (Exception e)
                            {
                                BuffPlugin.LogException(e);
                                BuffPlugin.LogError($"Invoke {type.Name}.OnEnable Failed!");
                            }
                        }
                    }
                }

                try
                {
                    var runtimeAss = BuffBuilder.FinishGenerate(CurrentModId);
                    if (runtimeAss != null)
                    {
                        foreach (var name in currentRuntimeBuffName)
                        {
                            InternalRegisterBuff(new BuffID(name),
                                runtimeAss.GetType($"{CurrentModId}.{name}Buff", true),
                                runtimeAss.GetType($"{CurrentModId}.{name}BuffData", true));
                        }
                    }
                }
                catch (Exception e)
                {
                    BuffPlugin.LogException(e,$"Exception when load {mod.id}'s RuntimeBuff");
                }
                currentRuntimeBuffName.Clear();
            }
            CurrentModId = string.Empty;
        }


        /// <summary>
        /// 给BuffData设置config的warpper
        /// </summary>
        internal static void BuildAllDataStaticWarpper()
        {
            foreach (var dataType in DataTypes)
            {
                if (!BuffConfigManager.ContainsId(dataType.Key))
                {
                    BuffPlugin.LogError($"Can't find json data for ID :{dataType.Key}!");
                    continue;
                }
                foreach (var property in dataType.Value.GetProperties().
                             Where(i => i.GetCustomAttribute<CustomBuffConfigAttribute>(true) != null))
                {
                    //不存在get方法
                    if (property.GetGetMethod() == null)
                    {
                        BuffPlugin.LogError($"Property {property.Name} at Type {dataType.Value.Name} has no get method!");
                        continue;
                    }

                    //有set属性
                    if (property.CanWrite)
                        BuffPlugin.LogWarning($"Property {property.Name} can write!");

                    //读取特性
                    var configAttribute = property.GetCustomAttribute<CustomBuffConfigAttribute>();
                    var infoAttribute = property.GetCustomAttribute<CustomBuffConfigInfoAttribute>();//可为null
                    var bindConfigurable = BuffConfigurableManager.TryGetConfigurable(dataType.Key, property.Name, true, property.PropertyType, configAttribute.defaultValue);
                    bindConfigurable.acceptable = BuffConfigurableManager.GetProperAcceptable(configAttribute);

                    if (infoAttribute != null)
                    {
                        bindConfigurable.name = infoAttribute.name;
                        bindConfigurable.description = infoAttribute.description;
                    }
                    else
                    {
                        bindConfigurable.name = property.Name;
                        bindConfigurable.description = "";
                    }

                    var hook = new ILHook(property.GetGetMethod(), (il) =>
                    {
                        il.Instrs.Clear();
                        il.Body.MaxStackSize += 1;
                        ILCursor c = new ILCursor(il);
                        c.Emit(OpCodes.Ldarg_0);
                        c.EmitDelegate<Func<BuffData, object>>((self) =>
                        {
                            BuffPlugin.Log($"property get : {bindConfigurable.name}-{bindConfigurable.key}, {bindConfigurable.BoxedValue}");
                            return bindConfigurable.BoxedValue;
                        });
                        if (property.PropertyType.IsValueType)
                            c.Emit(OpCodes.Unbox_Any, property.PropertyType);
                        else
                            c.Emit(OpCodes.Castclass, property.PropertyType);
                        c.Emit(OpCodes.Ret);
                    });

                    if (BuffPlugin.DevEnabled)
                        BuffPlugin.Log($"Warp config property for {dataType.Key} : {property.Name} : {property.PropertyType}");
                    
                }
            }
        }

        public class ConditionType
        {
            public Type Type { get; }
            public ConditionType Parent { get; }
            public string DisplayName { get; }

            public ConditionID Id { get; }

            public ConditionType(ConditionID id, Type type, ConditionType parent, string displayName)
            {
                Id = id;
                Type = type;
                Parent = parent;
                DisplayName = displayName;
            }

            public bool CanUseInCurrentTemplate(GachaTemplateID id)
            {
                var type = GetTemplateType(id);
                var con = this;
                while (con != null)
                {
                    if (type.BanConditionIds.Contains(con.Id))
                        return false;
                    con = con.Parent;
                }

                return true;
            }
        }

        public class GachaTemplateType
        {
            public Type Type { get; }
            public HashSet<ConditionID> BanConditionIds { get; }

            public GachaTemplateType(Type type, HashSet<ConditionID> banConditionIds)
            {
                this.Type = type;
                BanConditionIds = banConditionIds;
            }
        }

        private static readonly Dictionary<BuffID, Type> DataTypes = new();
        private static readonly Dictionary<BuffID, Type> BuffTypes = new();

        private static readonly Dictionary<GachaTemplateID, GachaTemplateType> TemplateTypes = new();
        private static readonly Dictionary<ConditionID, ConditionType> ConditionTypes = new();

    }
}
