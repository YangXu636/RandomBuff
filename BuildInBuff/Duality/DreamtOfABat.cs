using BuiltinBuffs.Positive;
using HotDogGains.Duality;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RandomBuff;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BuiltinBuffs.Negative;
using UnityEngine;

namespace BuildInBuff.Duality
{
    class DreamtOfABatBuff : Buff<DreamtOfABatBuff, DreamtOfABatBuffData> { public override BuffID ID => DreamtOfABatBuffEntry.DreamtOfABatID; }
    class DreamtOfABatBuffData : CountableBuffData
    {
        public override BuffID ID => DreamtOfABatBuffEntry.DreamtOfABatID;
        public override bool CanStackMore() => StackLayer < 4;

        public override int MaxCycleCount => 3;

    }
    class DreamtOfABatBuffEntry : IBuffEntry
    {
        public static BuffID DreamtOfABatID = new BuffID("DreamtOfABatID", true);
        public void OnEnable()
        {
            BuffRegister.RegisterBuff<DreamtOfABatBuff, DreamtOfABatBuffData, DreamtOfABatBuffEntry>(DreamtOfABatID);
        }
        public static void HookOn()
        {
            //晕眩的时候变成蝙蝠
            On.Player.Stun += Player_Stun;
            //防止消失时被判死亡
            On.Player.Die += Player_Die;


            //改变玩家蝙蝠的颜色
            On.FlyGraphics.ApplyPalette += ButteFly_ApplyPalette;

            //防止溶光消灭玩家的蝙蝠
            IL.MeltLights.Update += MeltLights_Update;
        }

        private static void MeltLights_Update(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                (i) => i.MatchLdarg(0),
                (i) => i.MatchLdfld<UpdatableAndDeletable>("room"),
                (i) => i.MatchLdfld<Room>("physicalObjects"),
                (i) => i.MatchLdcI4(0),
                (i) => i.MatchLdelemRef(),
                (i) => i.MatchLdloc(1),
                (i) => i.Match(OpCodes.Callvirt)
                ))
            {
                c.EmitDelegate<Func<PhysicalObject, PhysicalObject>>((obj) =>
                {
                    if (obj is Fly fly && fly.IsButterFly())
                    {
                        return null;//如果是梦蝶就传个空值
                    }
                    return obj;
                });
            }
        }

        private static void ButteFly_ApplyPalette(On.FlyGraphics.orig_ApplyPalette orig, FlyGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig.Invoke(self, sLeaser, rCam, palette);

            //让蝙蝠身体的颜色和玩家的颜色一样
            if (self.fly.IsButterFly(out var butteFly))
            {
                for (int i = 0; i < 3; i++)
                {
                    sLeaser.sprites[i].color = butteFly.color;
                }
            }

        }


        private static void Player_Die(On.Player.orig_Die orig, Player self)
        {
            if (self.stun > 0 && self.slatedForDeletetion)
            {
                foreach (var item in self.room.updateList)
                {
                    if (item is BatBody body && body.player == self)
                    {
                        return;
                    }
                }
            }

            orig.Invoke(self);
        }


        private static void Player_Stun(On.Player.orig_Stun orig, Player self, int st)
        {
            orig.Invoke(self, st);

            if (self.dead) return;

            if (self.room != null && self.room.updateList != null)
            {
                //fp房间内不发动卡牌防止卡死
                if (self.room.abstractRoom.name.Length > 2 && self.room.abstractRoom.name.Substring(self.room.abstractRoom.name.Length - 2) == "AI") return;
                if (self.room.abstractRoom.name == "SB_E05SAINT") return;


                //已经
                foreach (var item in self.room.updateList)
                {
                    if (item is BatBody body && body.player == self)
                    {
                        return;
                    }
                }

                //稍微添加一点阈值防止莫名其妙的发动卡牌
                var activeLimite = 12 - (DreamtOfABatID.GetBuffData().StackLayer > 2 ? (DreamtOfABatID.GetBuffData().StackLayer - 2) * 5 : 0);
                //虚弱状态更难发动变蝙蝠
                activeLimite *= self.exhausted ? 2 : 1;
                if (st > activeLimite) self.room.AddObject(new BatBody(self.abstractCreature, HeartDevouringWormBuffEntry.IsInfected(self)));
            }


        }
    }

    public class BatBody : UpdatableAndDeletable
    {

        public AbstractCreature absPlayer;
        public Player player => absPlayer.realizedCreature as Player;

        public Fly batBody;

        private bool dieAfterDestroy;

        public BatBody(AbstractCreature absPlayer, bool dieAfterDestroy)
        {
            DreamtOfABatBuff.Instance.TriggerSelf(true);
            this.dieAfterDestroy = dieAfterDestroy;
            this.absPlayer = absPlayer;


            //召唤改色蝙蝠
            var room = player.room;
            var absFly = new AbstractCreature(room.world, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly), null, room.GetWorldCoordinate(player.DangerPos), room.world.game.GetNewID());

            absFly.TurnButteFLy(absPlayer.realizedCreature.ShortCutColor());

            room.abstractRoom.AddEntity(absFly);
            absFly.RealizeInRoom();

            batBody = absFly.realizedCreature as Fly;

            //移动蝙蝠
            batBody.firstChunk.HardSetPosition(player.firstChunk.pos);
            batBody.firstChunk.vel += player.firstChunk.vel;


            //batBody.abstractCreature.controlled=true;

            //光效
            AddEffect(room);

            if ((ModManager.MSC || ModManager.CoopAvailable) && player.slugOnBack != null && player.slugOnBack.slugcat != null)
            {
                player.slugOnBack.DropSlug();
            }
            if (player.spearOnBack != null && player.spearOnBack.spear != null)
            {
                player.spearOnBack.DropSpear();
            }

            //让房间自动删除玩家
            player.slatedForDeletetion = true;

            player.wantToPickUp = 0;

        }

        public override void Destroy()
        {
            if (player.slatedForDeletetion && !batBody.slatedForDeletetion)
            {
                player.slatedForDeletetion = false;

                //防止重复添加玩家
                bool notHavePlayer = true;
                //检查玩家是否已经被复活
                foreach (var item in room.abstractRoom.creatures)
                {
                    if (item == player.abstractCreature) notHavePlayer = false;
                }

                //重现玩家
                if (notHavePlayer)
                {

                    //如果玩家没有就创造一个玩家
                    //room.abstractRoom.AddEntity(player.abstractCreature);
                    //player.PlaceInRoom(room);
                    var absPlayer = player.abstractCreature;

                    if (!room.abstractRoom.creatures.Contains(absPlayer))
                        room.abstractRoom.AddEntity(absPlayer);

                    if (!room.abstractRoom.realizedRoom.updateList.Contains(player))
                        room.abstractRoom.realizedRoom.AddObject(player);


                    //让玩家到蝙蝠位置
                    for (int i = 0; i < player.bodyChunks.Length; i++)
                    {
                        //player.bodyChunks[i].HardSetPosition(batBody.firstChunk.pos);

                        //player.bodyChunks[i].vel = batBody.firstChunk.vel;
                    }
                    //让玩家能站着
                    player.standing = true;
                    if (dieAfterDestroy)
                    {
                        player.Die();
                        player.abstractCreature.state.meatLeft = 0;
                    }

                    player.graphicsModule.Reset();
                }

            }
            if (batBody.dead || batBody.slatedForDeletetion) player.Die();

            batBody.Destroy();
            base.Destroy();

        }

        public void AddEffect(Room room)
        {
            room.AddObject(new Explosion.ExplosionLight(player.firstChunk.pos, 80, 1, 20, Custom.hexToColor("93c5d4")));
            room.AddObject(new SporePlant.BeeSpark(player.firstChunk.pos));
        }

        public override void Update(bool eu)
        {
            base.Update(eu);

            //防止进入管道
            batBody.enteringShortCut = null;
            batBody.shortcutDelay = 40;

            if (player != null)
            {
                if (player.dead) batBody.dead = true;

                
                if (batBody.Consious)
                {
                    if (player.airInLungs > 0)
                    {
                        player.airInLungs -= 1f / (40f * (player.lungsExhausted ? 4.5f : 9f) * ((float)this.room.game.setupValues.lungs / 100f)) * player.slugcatStats.lungsFac * 2;
                        batBody.drown = 0;
                    }

                    if (DreamtOfABatBuffEntry.DreamtOfABatID.GetBuffData().StackLayer > 1)
                    {
                        batBody.abstractCreature.controlled = true;
                        batBody.inputWithDiagonals = RWInput.PlayerInput(player.playerState.playerNumber);
                    }
                }


                if (batBody.slatedForDeletetion) player.stun = 0;

                if (player.stun <= 0) this.Destroy();
                else
                {
                    player.stun--;
                    player.AerobicIncrease(0.1f);
                    //让玩家到蝙蝠位置
                    for (int i = 0; i < player.bodyChunks.Length; i++)
                    {
                        player.bodyChunks[i].HardSetPosition(batBody.firstChunk.pos);
                        player.bodyChunks[i].vel = batBody.firstChunk.vel;
                    }
                }
            }


        }


    }

    public static class EXFly
    {
        public static bool IsButterFly(this Fly fly) => ButteFly.modules.TryGetValue(fly.abstractCreature, out var butteFly);
        public static bool IsButterFly(this Fly fly, out ButteFly butteFly) => ButteFly.modules.TryGetValue(fly.abstractCreature, out butteFly);

        public static void TurnButteFLy(this AbstractCreature fly, Color color)
        {
            ButteFly.modules.Add(fly, new ButteFly(color));
        }
    }

    public class ButteFly
    {
        public static ConditionalWeakTable<AbstractCreature, ButteFly> modules = new ConditionalWeakTable<AbstractCreature, ButteFly>();

        public Color color;
        public ButteFly(Color color) { this.color = color; }
    }
}