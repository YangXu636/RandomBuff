﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx;
using RandomBuff.Core.Entry;
using RandomBuff.Core.Game;
using RandomBuff.Core.Hooks;
using RandomBuff.Core.SaveData;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

//添加友元方便调试
[assembly: InternalsVisibleTo("BuffTest")]

namespace RandomBuff
{
    [BepInPlugin("randombuff", "Random Buff", "1.0.0")]
    public class BuffPlugin : BaseUnityPlugin
    {
        public const string saveVersion = "a-0.0.2";

        public void OnEnable()
        {
            try
            {
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.RainWorld.PostModsInit += RainWorld_PostModsInit;
            }
            catch (Exception e)
            {
                Logger.LogFatal(e.ToString());
            }
        }

        private void Update()
        {
            Render.CardRender.CardRendererManager.UpdateInactiveRendererTimers(Time.deltaTime);
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {

            try
            {
                if (!isLoaded)
                    File.Create(AssetManager.ResolveFilePath("randomBuff.log")).Close();

            }
            catch (Exception e)
            {
                canAccessLog = false;
                Logger.LogFatal(e.ToString());
                Debug.LogException(e);
            }
          
            try
            {
                orig(self);
            }
            catch (Exception e)
            {
                LogException(e);
            }
            try
            {
                if (!isLoaded)
                {
                    Log($"[Random Buff], version: {saveVersion}");

                    if (File.Exists(AssetManager.ResolveFilePath("buff.dev")))
                    {
                        DevEnabled = true;
                        LogWarning("Debug Enable");
                    }
                    Render.CardRender.CardBasicAssets.LoadAssets();

                    BaseGameSetting.Init();
                    BuffFile.OnModsInit();
                    CoreHooks.OnModsInit();
                    //HooksApplier.ApplyHooks();
                    BuffRegister.InitAllBuffPlugin();
               
                    isLoaded = true;
                }
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            try
            {
                orig(self);
            }
            catch (Exception e)
            {
                LogException(e);
            }
            try
            {
                if (!isPostLoaded)
                {
                    //延迟加载以保证其他plugin的注册完毕后再加载
                    BuffConfigManager.InitBuffStaticData();
                    BuffRegister.BuildAllDataStaticWarpper();
                    isPostLoaded = true;
                }
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        private static bool isLoaded = false;
        private static bool isPostLoaded = false;
        private static bool canAccessLog = true;

        internal static bool DevEnabled { get; private set; }


        /// <summary>
        /// 会额外保存到../RainWorld_Data/StreamingAssets/randomBuff.log
        /// </summary>
        /// <param name="message"></param>
        public static void Log(object message)
        {
            Debug.Log($"[RandomBuff] {message}");
            if(canAccessLog)
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Message]\t{message}\n");
           
        }

        public static void LogDebug(object message)
        {
            if (DevEnabled)
            {
                Debug.Log($"[RandomBuff] {message}");
                if (canAccessLog)
                    File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Debug]\t\t{message}\n");
            }

        }

        public static void LogWarning(object message)
        {
            Debug.LogWarning($"[RandomBuff] {message}");
            if (canAccessLog)
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Warning]\t{message}\n");
        }

        public static void LogError(object message)
        {
            Debug.LogError($"[RandomBuff] {message}");
            if (canAccessLog)
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Error]\t\t{message}\n");
        }

        public static void LogException(Exception e)
        {
            Debug.LogException(e);
            if (canAccessLog)
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Fatal]\t\t{e.Message}\n");
          
        }

        public static void LogException(Exception e,object m)
        {
            Debug.LogException(e);
            if (canAccessLog)
            {
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Fatal]\t\t{e.Message}\n");
                File.AppendAllText(AssetManager.ResolveFilePath("randomBuff.log"), $"[Fatal]\t\t{m}\n");

            }


        }
    }

    /// <summary>
    /// 用来简化应用钩子的过程（懒得自己写了）
    /// 继承这个类并且编写一个名为 HooksOn 的公共静态方法即可
    /// </summary>
    internal class HooksApplier
    {
        internal static void ApplyHooks()
        {
            var applierType = typeof(HooksApplier);
            var types = applierType.Assembly.GetTypes().Where(a => a.BaseType == applierType && a != applierType);
            foreach (var t in types)
            {
                try
                {
                    t.GetMethod("HooksOn", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
