using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;


using Newtonsoft.Json;

using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Script;

using Dalamud.Utility.Numerics;
using ECommons;


using Util = TsingNamespace.Dawntrail.Savage.M7S.Utilities_Tsing;
using EX = TsingNamespace.Dawntrail.Savage.M7S.ScriptExtensions_Tsing;

namespace TsingNamespace.Dawntrail.Savage.M7S
{
    [ScriptType(name: "M7S·阿卡狄亚零式·中量级3", guid: "e3cfc380-edc2-f441-bebe-e9e294f2631f", territorys: [1261], version: "0.0.0.9", author: "Mao", note: noteStr)]
    public class M7S_Script
    {

        /*
            DONE boss冲锋技能危险区预警
            CANCEL 第二轮冰花站位预指路
            DONE P3 孢子安全区指路
            DONE 孢囊的地火危险区高亮
        */
        const string noteStr =
        """
            【宝宝椅用户请着重阅读栏目3和4】
            1.指路部分目前已适配国服MMW攻略
              如果你没有开启其他插件，通常默认设置已经足够通关国服野队
            2.启用了"设置 - 绘制 - 【仅绘制强制指定为Imgui模式的元素】"功能的用户，
              请将脚本页面中 用户设置 栏目最下侧 指路时使用的绘图类型 调整为Imgui
            3.关于【P2P3冰花没有着色指引或者指路绘图】的情况，请确保相关触发器为启用状态(两两冰花/两轮冰花)
              出现相关问题可以勾选 
              用户设置最下侧 P3两轮冰花Debug模式
              将会在默语频道发送简易logs信息，可以在问题反馈时附上截图
              如果安装了bby，可以尝试关闭bby后前往dalamud插件界面关开可达鸭插件。
            4.关于长时间战斗后，【绘图概率不显示】的用户，也可以尝试上述操作。
            5.如果你启用了实验性功能后出现游戏掉线或者客户端崩溃/闪退等情况
              请尝试关闭头顶标记功能，或者关闭所有实验性功能。
        """;


        [UserSetting("默认职能顺序")]
        public PlayerRoleListEnum RoleMarks8 { get; set; } = PlayerRoleListEnum.MT_ST_H1_H2_D1_D2_D3_D4;
        public enum PlayerRoleListEnum
        {
            MT_ST_H1_H2_D1_D2_D3_D4,
        }

        [UserSetting("攻略类型")]
        public WalkthroughEnum WalkthroughType { get; set; } = WalkthroughEnum.MMW_SPJP;
        public enum WalkthroughEnum { MMW_SPJP }

        [UserSetting("实验性功能：启用小怪自动挑衅(小怪组别跟随攻略)")]
        public bool AutoProvokeWildwindsMobsEnable { get; set; } = false;
        public enum 挑衅策略 { 刷新时挑衅较远的小怪, 刷新8秒后挑衅非引战小怪 }
        [UserSetting("实验性功能：小怪自动挑衅策略")]
        public 挑衅策略 AutoProvokeStrategy { get; set; } = 挑衅策略.刷新8秒后挑衅非引战小怪;

        [UserSetting("实验性功能：启用小怪自动打断(打断目标跟随攻略)")]
        public bool AutoInterruptWildwindsMobsEnable { get; set; } = false;
        [UserSetting("实验性功能：启用坦克自动支援减")]
        public bool AutoTankSupportEnable { get; set; } = false;
        [UserSetting("实验性功能：小怪相关机制启用头顶标记功能")]
        public bool AutoMobsMarkEnable { get; set; } = true;


        [UserSetting("P2冰花着色 => 类型: 奇数轮次")]
        public ScriptColor StrangeSeedsCountOdd { get; set; } = new() { V4 = new(0, 1, 1, 2) };
        [UserSetting("P2冰花着色 => 类型: 偶数轮次")]
        public ScriptColor StrangeSeedsCountEven { get; set; } = new() { V4 = new(1, 1, 0, 2) };
        [UserSetting("P2冰花着色的颜色深度")]
        public float P2StrangeSeedsColorDensity { get; set; } = 2;
        [UserSetting("P2冰花站位提示简约风格")]
        public bool P2StrangeSeedsSimpleStyle { get; set; } = true;
        [UserSetting("P2冰花调整为固定式")]
        public bool P2StrangeSeedsFixed { get; set; } = false;
        [UserSetting("P2启用野蛮怒视指路")]
        public bool P2GlowerPowerGuideDrawEnabled { get; set; } = true;

        [UserSetting("P3第二轮冰花MMW使用追车站位")]
        public bool P3MMWZhuiChe { get; set; } = true;


        [UserSetting("指路时使用的颜色 => 类型: 立即前往")]
        public ScriptColor GuideColor_GoNow { get; set; } = new() { V4 = new(0, 1, 1, 2) };
        [UserSetting("指路时使用的颜色 => 类型: 稍后前往")]
        public ScriptColor GuideColor_GoLater { get; set; } = new() { V4 = new(1, 1, 0, 2) };
        [UserSetting("指路时使用的颜色深度")]
        public float GuideColorDensity { get; set; } = 2;
        [UserSetting("Vfx绘制时指路特效的宽度")]
        public float Guide_Width { get; set; } = 1.4f;

        [UserSetting("指路时使用的绘图类型")]
        public DrawModeEnum GuideDrawMode { get; set; } = DrawModeEnum.Imgui;

        [UserSetting("特殊项目: P3两轮冰花Debug模式")]
        public bool P3MMWZhuiCheDebug { get; set; } = false;
        [UserSetting("特殊项目: 石化波动安全区强制使用Imgui绘制")]
        public bool QuarrySwampSafeZoneImgui { get; set; } = false;


        private static readonly object _lock = new object();
        private List<IGameObject> WildwindsMobs = new List<IGameObject>();
        private Dictionary<ulong, Vector3> WildwindsMobsBornPos = new Dictionary<ulong, Vector3>();
        private List<ulong> SinisterSeedTargets = new List<ulong>(); // P1/P3 的冰花目标列表, 用于标记冰花目标
        private uint ExplosionCount = 0;
        private uint StrangeSeedsCount = 0;
        private readonly EX.MultiDisDrawProp MultiDisProp = new();
        private uint P2_BrutishSwingCastedCount = 0;
        private uint P3_BrutishSwingCastedCount = 0;
        private uint P3_StoneringerId = 0;




        public void Init(ScriptAccessory accessory)
        {
            // LatestStoneringerId = 0;
            WildwindsMobs.Clear();
            WildwindsMobsBornPos.Clear();
            SinisterSeedTargets.Clear();
            ExplosionCount = 0;
            StrangeSeedsCount = 0;

            P2_BrutishSwingCastedCount = 0;
            P3_BrutishSwingCastedCount = 0;
            P3_StoneringerId = 0;
            accessory.Method.RemoveDraw(".*");
            accessory.Log.Debug($"M7S Script Init");


            MultiDisProp.Color_GoNow = GuideColor_GoNow.V4.WithW(GuideColorDensity);
            MultiDisProp.Color_GoLater = GuideColor_GoLater.V4.WithW(GuideColorDensity);
            MultiDisProp.Width = Guide_Width;
            MultiDisProp.EndCircleRadius = Guide_Width * 0.5f + 0.05f;
            MultiDisProp.DrawMode = GuideDrawMode;

        }


        [ScriptMethod(name: "野蛮碎击初始化动作",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutalImpactActionId],
            userControl: false)]
        public void BrutalImpactInit(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y < -100) return; // 只在P1阶段执行
            Init(accessory);
        }

        [ScriptMethod(name: "死刑 远/近侧挥打 危险区绘制 Smash Here/There Dangerous Zone Draw",
                    eventType: EventTypeEnum.StartCasting,
                    eventCondition: [DataM7S.SmashHereThereActionId])]
        public void SmashHereThereDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            long destoryAt = 3000 + 900;
            uint actionId = @event.ActionId;
            ulong bossId = @event.SourceId;
            float radius_Smash = 6.0f;
            (long, long) delay_destoryAt = new(0, destoryAt);

            /* 
            打近打远
            思路1 仅标记死刑范围
            思路2 标记死刑范围，同时非坦克职业标记应该靠近还是远离BOSS Circle + 箭头
            */
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = nameof(SmashHereThereDangerousZoneDraw) + Guid.NewGuid().ToString();
            dp.Scale = new Vector2(radius_Smash, radius_Smash);
            dp.Delay = 0;
            dp.DestoryAt = destoryAt + 1550;
            dp.Owner = bossId;
            dp.CentreResolvePattern = actionId == (uint)DataM7S.AID.SmashHere ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            //非坦克职能标记安全方向, 坦克职能标记危险方向
            bool IsMeTank = accessory.Data.MyObject.IsTank();
            DrawPropertiesEdit dpArrow = accessory.Data.GetDefaultDrawProperties();
            dpArrow.Name = nameof(SmashHereThereDangerousZoneDraw) + "Arrow" + Guid.NewGuid().ToString();
            dpArrow.Delay = dp.Delay;
            dpArrow.DestoryAt = dp.DestoryAt;
            dpArrow.Scale = new Vector2(1, 3);
            dpArrow.Owner = accessory.Data.Me;
            dpArrow.TargetObject = bossId;
            dpArrow.Color = IsMeTank ? accessory.Data.DefaultDangerColor.WithW(0.5f) : accessory.Data.DefaultSafeColor.WithW(0.5f);
            dpArrow.Rotation = (IsMeTank ? MathF.PI : 0) + (actionId == (uint)DataM7S.AID.SmashHere ? MathF.PI : 0);
            accessory.Method.SendDraw(MultiDisProp.DrawMode, DrawTypeEnum.Arrow, dpArrow);
        }

        [ScriptMethod(name: "死刑时 野蛮横扫 危险区绘制 Brutish Swing With Smash Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P1])]
        public void BrutishSwingWithSmashDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            uint actionId = @event.ActionId;
            ulong bossId = @event.SourceId;
            long destoryAt = 4000 - 700;
            (long, long) delay_destoryAt = new(0, destoryAt);
            float radius_Stick = 12.0f;
            float radius_Machete = 9.0f;
            switch (actionId)
            {
                case (uint)DataM7S.AID.BrutishSwingStick_P1:
                    accessory.FastDraw(DrawTypeEnum.Circle, bossId, new Vector2(radius_Stick, radius_Stick), delay_destoryAt, false);
                    break;
                case (uint)DataM7S.AID.BrutishSwingMachete_P1:
                    accessory.FastDraw(DrawTypeEnum.Donut, bossId, new Vector2(radius_Machete * 4, radius_Machete), delay_destoryAt, false);
                    break;
            }

        }

        [ScriptMethod(name: "P1/P3 种弹播撒 危险区绘制 Pollen Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.PollenActionId])]
        public void P1_PollenDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.EffectPosition;
            long destoryAt = 3600;
            (long, long) delay_destoryAt = new(0, destoryAt);
            float radius_AOE = 8.0f;
            accessory.FastDraw(DrawTypeEnum.Circle, pos, new Vector2(radius_AOE, radius_AOE), delay_destoryAt, false);
            // DONE 是否可以在孢囊实体出现的时刻，就知道安全区的类型？<= 好像不行，孢囊只有动画没有实体
        }
        [ScriptMethod(name: "P1 种弹播撒(孢囊) 指路绘制一 Pollen Guide Draw 1",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.PollenActionId])]
        public void P1_PollenGuideDraw1(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.EffectPosition;
            if (pos.Y < -100) return; // 只在P1阶段绘制
            // 只检测最靠近左下角或者右下角的那个AOE圈
            if (Util.DistanceByTwoPoints(pos, DataM7S.P1_FieldCenter) < 22.5f
                || pos.Z < DataM7S.P1_FieldCenter.Z) return;
            SinisterSeedTargets.Clear();
            bool isLeftDownSafe = pos.X > DataM7S.P1_FieldCenter.X;
            // Vector3 myStartPos = DataM7S.P1_FieldCenter;
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            Vector3 myStartPos = (myRole, WalkthroughType, isLeftDownSafe) switch
            {
                // MT, ST, D1, D2 共用偏移
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(7, 0, 7),
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-7, 0, 7),

                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(7, 0, 7),
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-7, 0, 7),

                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(7, 0, 7),
                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-7, 0, 7),

                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(7, 0, 7),
                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-7, 0, 7),

                // H1 & H2 使用同一个偏移
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(-17, 0, 17),
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(17, 0, 17),

                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(-17, 0, 17),
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(17, 0, 17),

                // D3 & D4
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(17, 0, -17),
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-17, 0, -17),

                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, true) => DataM7S.P1_FieldCenter + new Vector3(17, 0, -17),
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, false) => DataM7S.P1_FieldCenter + new Vector3(-17, 0, -17),

                // 默认位置（其他情况）
                _ => DataM7S.P1_FieldCenter
            };

            // 先指到安全区, 再指到第一次爆炸后去的预点位
            // accessory.FastDraw(DrawTypeEnum.Circle, myStartPos + new Vector3(0, 0, 5), new Vector2(1, 1), new(0, 5000), false);
            EX.DisplacementContainer myStartPosDC = new(myStartPos, 0, 5000 - 1060);
            switch (WalkthroughType)
            {
                case WalkthroughEnum.MMW_SPJP:
                    float modLength = 10;
                    // X 轴向场中偏移一个modLength
                    Vector3 myModPos = new(Math.Sign(DataM7S.P1_FieldCenter.X - myStartPos.X) * modLength + myStartPos.X, myStartPos.Y, myStartPos.Z);
                    EX.DisplacementContainer myModPosDC = new(myModPos, 0, 900);
                    accessory.MultiDisDraw(new List<EX.DisplacementContainer> { myStartPosDC, myModPosDC }, MultiDisProp);
                    break;
            }


        }


        [ScriptMethod(name: "P1 荆棘蔓延 危险区预绘制 Roots Of Evil Dangerous Zone Pre Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsChaseActionId])]
        public void P1_RootsOfEvilDangerousZonePreDraw(Event @event, ScriptAccessory accessory)
        {
            // 多个潜地跑，使用用一个时间戳
            Vector3 pos = @event.EffectPosition;
            lock (_lock)
            {
                if ((DateTime.Now > ChaseDisplayTime))
                {
                    // 说明ChaseDisplayTime没更新
                    if (pos.Y > -100)
                    {
                        ChaseDisplayTime = DateTime.Now + TimeSpan.FromSeconds(11.5);
                    }
                    else
                    { 
                        ChaseDisplayTime = DateTime.Now + TimeSpan.FromSeconds(8.1);
                    }
                    
                    // 第一次地火的十秒后画预警
                }
            }
            long delay = (long)(ChaseDisplayTime - DateTime.Now).TotalMilliseconds;
            long destoryAt = 4000;
            (long, long) delay_destoryAt = new(delay, destoryAt);
            float radius_AOE = 12.0f;
            accessory.FastDraw(DrawTypeEnum.Circle, pos, new Vector2(radius_AOE, radius_AOE), delay_destoryAt, accessory.Data.DefaultDangerColor.WithW(0.4f));
        }
        private DateTime ChaseDisplayTime = DateTime.Now;


        [ScriptMethod(name: "P1/P3 荆棘蔓延 危险区绘制 Roots Of Evil Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.RootsOfEvilActionId])]
        public void P1_RootsOfEvilDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.EffectPosition;
            long destoryAt = 3000;
            (long, long) delay_destoryAt = new(0, destoryAt);
            float radius_AOE = 12.0f;
            accessory.FastDraw(DrawTypeEnum.Circle, pos, new Vector2(radius_AOE, radius_AOE), delay_destoryAt, false);
        }

        [ScriptMethod(name: "P1 种弹播撒(紫圈冰花) 危险区绘制 Sinister Seeds Blossom Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsBlossomActionId])]
        public void P1_SinisterSeedsBlossomDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            // 目标脚下放置米字型冰花 , P3 屏蔽，会影响看地板花纹
            // 冰花的落点判定似乎要比前置技能的读条结束时间要晚一点
            ulong tarId = @event.TargetId;
            if (tarId != accessory.Data.Me || @event.SourcePosition.Y < -100) return;

            // long destoryAt = (long)@event.DurationMilliseconds();
            long destoryAt = 7000; // P2的黄圈冰花 -1500ms
            if(@event.SourcePosition.Z < 50) destoryAt -= 1500;
            float radius_AOE = 4f;
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(radius_AOE, 20.0f);
            dp.Owner = tarId;
            dp.Delay = 0;
            dp.DestoryAt = destoryAt;
            dp.FixRotation = true;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.09f);
            for (int i = 0; i < 4; i++)
            {
                dp.Name = $"{nameof(P1_SinisterSeedsBlossomDangerousZoneDraw)}{i}" + Guid.NewGuid().ToString();
                dp.Rotation = MathF.PI * 0.25f * i;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }

        }

        [ScriptMethod(name: "P1 种弹播撒(紫圈冰花) 指路绘制 Sinister Seeds Blossom Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsBlossomActionId42350])]
        public void P1_SinisterSeedsBlossomGuideDraw(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y < -100) return; // 只在P1阶段绘制
            ulong tarId = @event.TargetId;
            int count = -1;
            lock (_lock)
            {
                if (!SinisterSeedTargets.Contains(tarId))
                {
                    SinisterSeedTargets.Add(tarId); // 记录冰花目标
                    count = SinisterSeedTargets.Count;
                }
            }
            if (count != 4 || @event.SourcePosition.Y < -100 || @event.SourcePosition.Z < 50) return;


            bool isMeGetSinisterSeed = SinisterSeedTargets.Contains((ulong)accessory.Data.Me);
            // long destoryAt = (long)@event.DurationMilliseconds();
            long destoryAt = 7000;
            float radius_AOE = 4f;

            // 先指到冰花放置点位, 再指到分摊点位，再指到场地西侧，仅在P1生效
            Vector3 myStartPos = DataM7S.P1_FieldCenter;
            Vector3 myModPos = DataM7S.P1_FieldCenter; //用于非紫圈玩家的指路
            Vector3 myStackPos = DataM7S.P1_FieldCenter;
            Vector3 myEndPos = DataM7S.P1_FieldCenter;


            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            Vector3 center = DataM7S.P1_FieldCenter;
            Vector3 offset = DataM7S.P1_NailOffset_In;
            Vector3 MMW_Offset_LeftStack = new Vector3(-6, 0, 0);
            Vector3 MMW_Offset_RightStack = new Vector3(6, 0, 0);
            Vector3 MMW_Offset_EndPos = new Vector3(-18, 0, 0);

            (myStartPos, myModPos, myStackPos, myEndPos) = (myRole, WalkthroughType, isMeGetSinisterSeed) switch
            {
                // ↓ 被点了冰花
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(-offset.X, 0, -offset.Z), myModPos, center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(offset.X, 0, -offset.Z), myModPos, center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(-offset.X, 0, offset.Z), myModPos, center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(offset.X, 0, offset.Z), myModPos, center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(-offset.X, 0, offset.Z), myModPos, center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(offset.X, 0, offset.Z), myModPos, center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(-offset.X, 0, -offset.Z), myModPos, center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, true) =>
                    (center + new Vector3(offset.X, 0, -offset.Z), myModPos, center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                // ↓ 没被点冰花
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -1), center + new Vector3(0, 0, -9), center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -1), center + new Vector3(0, 0, -9), center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, 18), center + new Vector3(0, 0, 9), center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, 18), center + new Vector3(0, 0, 9), center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -1), center + new Vector3(0, 0, -9), center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -1), center + new Vector3(0, 0, -9), center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -18), center + new Vector3(0, 0, -9), center + MMW_Offset_LeftStack, center + MMW_Offset_EndPos),

                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, false) =>
                    (center + new Vector3(0, 0, -18), center + new Vector3(0, 0, -9), center + MMW_Offset_RightStack, center + MMW_Offset_EndPos),

                _ => (myStartPos, myModPos, myStackPos, myEndPos)
            };
            switch (WalkthroughType)
            {
                case WalkthroughEnum.MMW_SPJP:
                    if (isMeGetSinisterSeed)
                    {
                        accessory.MultiDisDraw(new List<EX.DisplacementContainer>
                        {
                            new(myStartPos, 0, destoryAt + 1200),
                            new(myStackPos, 0, 4000),
                            new(myEndPos, 0, 5000)
                        }, MultiDisProp);
                    }
                    else
                    {
                        accessory.MultiDisDraw(new List<EX.DisplacementContainer>
                        {
                            new(myStartPos, 800, 3200),
                            new(myModPos, 0, 2000),
                            new(myStackPos, 0, 5900),
                            new(myEndPos, 0, 5000)
                        }, MultiDisProp);
                    }

                    break;
            }

        }

        [ScriptMethod(name: "P1 荆棘缠缚 危险区绘制 Tendrils Of Terror Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.TendrilsOfTerrorActionId])]
        public void P1_TendrilsOfTerrorDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            // 目标放置米字型冰花
            Vector3 pos = @event.EffectPosition;
            // long destoryAt = (long)@event.DurationMilliseconds();
            long destoryAt = 3000;
            float radius_AOE = 4f;
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new Vector2(radius_AOE, 100.0f);
            dp.Position = pos;
            dp.Delay = 0;
            dp.DestoryAt = destoryAt;
            dp.FixRotation = true;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.8f);
            for (int i = 0; i < 4; i++)
            {
                dp.Name = $"{nameof(P1_TendrilsOfTerrorDangerousZoneDraw)}{i}" + Guid.NewGuid().ToString();
                dp.Rotation = MathF.PI * 0.25f * i;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
        }

        /*
            为读条月环的小怪标记攻击7和攻击8?
            为坦克选中最近的读条月环的小怪
            为远敏选中最近的读条月环的小怪?
        */
        [ScriptMethod(name: "P1 恨心花芽 加入战斗 Blooming Abomination Add Combatant",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: [DataM7S.BloomingAbominationDataId],
            userControl: false)]
        public void P1_BloomingAbominationAdd(Event @event, ScriptAccessory accessory)
        {
            // 记录一下ID和出生地点
            lock (_lock)
            {
                ulong mobId = @event.SourceId;
                WildwindsMobsBornPos[mobId] = @event.SourcePosition;
            }
        }

        [ScriptMethod(name: "P1/P3 恨心花芽 加入战斗时 自动挑衅 Blooming Abomination Add Combatant Auto Provoke",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: [DataM7S.BloomingAbominationDataId],
            suppress: 10000,
            userControl: true)]
        public async void P1P3_BloomingAbominationAddAutoProvoke(Event @event, ScriptAccessory accessory)
        {
            if (!AutoProvokeWildwindsMobsEnable) return;
            try
            {
                if (!accessory.Data.MyObject.IsTank()) return;
            }
            catch (System.Exception ex)
            {
                return;
            }
            await Task.Delay(2000); // 等待小怪加入战斗
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            List<IGameObject> mobs = accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BloomingAbomination).ToList();
            Vector3 fieldCenter = @event.SourcePosition.Y > -100 ? DataM7S.P1_FieldCenter : DataM7S.P3_FieldCenter;
            Vector3 myPrefPos = fieldCenter;

            myPrefPos = (myRole, WalkthroughType) switch
            {
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP) => fieldCenter + new Vector3(-30, 0, -30),
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP) => fieldCenter + new Vector3(30, 0, 30),
                _ => myPrefPos
            };
            mobs = mobs
                .OrderBy(mob => Util.DistanceByTwoPoints(mob.Position, myPrefPos)) // 按照距离排序
                .ToList();
            List<IGameObject> myMobs = new List<IGameObject>();
            if (mobs.Count <= 2)
            {
                myMobs = mobs;
            }
            else
            {
                // myMobs = myRole == EX.PlayerRoleEnum.MT ? mobs.Take(2).ToList() : mobs.TakeLast(2).ToList();
                myMobs = mobs.Take(2).ToList();
            }
            // 至此应该要拿到自己要拉的两只小怪了
            List<KodakkuAssist.Module.GameOperate.MarkType> markTypes = new List<KodakkuAssist.Module.GameOperate.MarkType>
            {
                KodakkuAssist.Module.GameOperate.MarkType.Stop1,
                KodakkuAssist.Module.GameOperate.MarkType.Stop2,
                KodakkuAssist.Module.GameOperate.MarkType.Bind1,
                KodakkuAssist.Module.GameOperate.MarkType.Bind2,
                KodakkuAssist.Module.GameOperate.MarkType.Bind3,
            };

            bool isMarkLocal = true;
            for (int i = 0; i < myMobs.Count; i++)
            {
                IGameObject _obj = myMobs[i];
                KodakkuAssist.Module.GameOperate.MarkType markType = markTypes[i];
                if (AutoMobsMarkEnable) accessory.Method.Mark(_obj.EntityId, markType, isMarkLocal);
                await Task.Delay(10); // 等待10毫秒
                // 标记一下
            }
            int delayTime = AutoProvokeStrategy switch
            {
                挑衅策略.刷新时挑衅较远的小怪 => 100,
                挑衅策略.刷新8秒后挑衅非引战小怪 => 6000,
                _ => 100,
            };
            await Task.Delay(delayTime); // 等待一段时间
            IGameObject? provokeTarget = null;
            switch (AutoProvokeStrategy)
            {
                case 挑衅策略.刷新时挑衅较远的小怪:
                    try
                    {
                        myMobs = myMobs.OrderBy(mob => Util.DistanceByTwoPoints(mob.Position, accessory.Data.MyObject.Position)).ToList();
                    }
                    finally
                    {
                        provokeTarget = myMobs.LastOrDefault();
                    }
                    break;
                case 挑衅策略.刷新8秒后挑衅非引战小怪:
                    try
                    {
                        List<IGameObject> nonTankMobs = mobs.Where(mob => mob.TargetObject is null || mob.TargetObject.EntityId != accessory.Data.Me).ToList();
                        // 而且我当前的目标不是这只小怪
                        nonTankMobs = nonTankMobs
                            .Where(mob => accessory.Data.MyObject.TargetObject is null || accessory.Data.MyObject.TargetObject.EntityId != mob.EntityId)
                            .ToList();
                        provokeTarget = myMobs.LastOrDefault();
                    }
                    catch
                    {
                        // nothing
                    }
                    // 只挑衅非引战小怪
                    break;
            }
            if (provokeTarget is not null)
            {
                Task.Run(async () =>
                    {
                        uint provokeActionId = 7533; // 挑衅技能ID
                        if (AutoMobsMarkEnable) accessory.Method.Mark(provokeTarget.EntityId, KodakkuAssist.Module.GameOperate.MarkType.Cross, isMarkLocal);
                        accessory.Log.Debug($"尝试挑衅小怪 {provokeTarget}");
                        accessory.Method.SendChat($"/e 尝试挑衅\"＋\"标记小怪 {provokeTarget} <se.5>");
                        for (int j = 0; j < 6; j++)
                        {
                            try
                            {
                                if (provokeTarget is IBattleChara _bc && !_bc.IsDead && !accessory.Data.MyObject.IsDead)
                                {
                                    accessory.Method.UseAction(provokeTarget.EntityId, provokeActionId);
                                    accessory.Log.Debug($"自动挑衅 => {accessory.GetMyRole()} to {provokeTarget}");
                                }
                                await Task.Delay(500); // 等待500毫秒
                            }
                            catch (System.Exception ex)
                            {
                                accessory.Log.Error($"自动挑衅异常 => {ex}");
                            }
                        }
                    });
            }
            await Task.Delay(6000);

            // 清除标记
            accessory.Method.SendChat($"/mk clear <stop1>");
            accessory.Method.SendChat($"/mk clear <stop2>");
            accessory.Method.SendChat($"/mk clear <cross>");
        }


        [ScriptMethod(name: "P1 小怪环形突风 自动打断 Mobs Winds Casting Mark and Interrupt",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.MobsWindsActionId])]
        public async void MobsWindsCastingMark(Event @event, ScriptAccessory accessory)
        {
            if (!AutoInterruptWildwindsMobsEnable) return;
            try
            {
                if (!accessory.Data.MyObject.IsTank()) return;
            }
            catch (System.Exception ex)
            {
                return;
            }
            uint actionId = @event.ActionId;
            ulong mobId = @event.SourceId;
            if (actionId != (uint)DataM7S.AID.WindingWildwinds) return;
            bool isGetTwoMobs = false;
            IGameObject mobObj = accessory.Data.Objects.SearchById(mobId);
            if (mobObj is not null)
            {
                lock (_lock)
                {
                    WildwindsMobs.Add(mobObj);
                    isGetTwoMobs = WildwindsMobs.Count == 2;
                }
            }
            if (!isGetTwoMobs) return;


            List<uint> castingMobs = new List<uint>();
            List<uint> rawHostileList = new List<uint>();

            try
            {
                unsafe
                {
                    // 获取仇恨列表中的小怪ID
                    for (int i = 0; i < 5; i++)
                    {
                        FFXIVClientStructs.FFXIV.Client.UI.Arrays.EnemyListNumberArray.EnemyListEnemyNumberArray* _hostileObj =
                            (FFXIVClientStructs.FFXIV.Client.UI.Arrays.EnemyListNumberArray.EnemyListEnemyNumberArray*)
                            ((byte*)FFXIVClientStructs.FFXIV.Client.UI.Arrays.EnemyListNumberArray.Instance() + 5 * 4 + i * (6 * 4));
                        accessory.Log.Debug($"Test: 在列表中获取敌人{i}  => {_hostileObj->EntityId} = {accessory.Data.Objects.SearchByEntityId((uint)(_hostileObj->EntityId))}");
                        if (_hostileObj->EntityId > 0x40_000_000)
                        {
                            rawHostileList.Add((uint)_hostileObj->EntityId);
                        }


                    }
                    castingMobs = rawHostileList
                        .Where(id => accessory.Data.Objects.SearchByEntityId(id) is IBattleChara bc && bc.IsCasting && bc.CastActionId == (uint)DataM7S.AID.WindingWildwinds)
                        .ToList();
                    // 过滤出正在读条的月环小怪ID
                }
            }
            catch (System.Exception ex)
            {
                accessory.Log.Error($"获取正在读条的月环小怪异常 => {ex}");
            }


            // 把出生地点靠近左上的小怪排到前边
            List<IGameObject> mobs = WildwindsMobs.OrderBy(obj =>
            {
                ulong id = obj.GameObjectId;
                Vector3 bornPos = obj.Position;
                // 去列表中查找出生地点
                if (WildwindsMobsBornPos.TryGetValue(id, out Vector3 _bornPos))
                {
                    bornPos = _bornPos;
                }
                Vector3 leftTop = DataM7S.P1_FieldCenter + new Vector3(-20f, 0, -20f);
                return Util.DistanceByTwoPoints(bornPos, leftTop);
            }).ToList();


            /*
              MT会拉到两只都读条月环的小怪?
            */

            KodakkuAssist.Module.GameOperate.MarkType markType = KodakkuAssist.Module.GameOperate.MarkType.Attack1;
            switch (WalkthroughType)
            {
                case WalkthroughEnum.MMW_SPJP:
                    // 标记

                    for (int i = 0; mobs.Count > 0 && i < mobs.Count; i++)
                    {
                        IGameObject _obj = mobs[i];

                        // 查找这个Obj的ID在 castingMobs 中的位置
                        int indexInHostileList = castingMobs.IndexOf(_obj.EntityId);
                        switch (indexInHostileList)
                        {
                            case -1:
                                // 不在列表中
                                markType = markType == KodakkuAssist.Module.GameOperate.MarkType.Stop1 ? KodakkuAssist.Module.GameOperate.MarkType.Stop1 : KodakkuAssist.Module.GameOperate.MarkType.Stop2;
                                break;
                            case 0:
                                markType = KodakkuAssist.Module.GameOperate.MarkType.Attack7;
                                break;
                            case 1:
                                markType = KodakkuAssist.Module.GameOperate.MarkType.Attack8;
                                break;
                        }
                        bool isLocal = true;
                        if (AutoMobsMarkEnable) accessory.Method.Mark(_obj.EntityId, markType, isLocal);
                        // 标记 + 打断
                        bool autoInterrupt = AutoInterruptWildwindsMobsEnable;
                        uint InterjectActionId = 7538;
                        uint HeadGrazeActionId = 7551;
                        if (autoInterrupt
                            && accessory.Data.MyObject is not null
                            && accessory.Data.MyObject.IsTank())
                        {
                            // 我是坦克，并且开启了自动打断功能
                            bool isMyMob = false;
                            switch (accessory.GetMyRole())
                            {
                                case EX.PlayerRoleEnum.MT:
                                    isMyMob = markType == KodakkuAssist.Module.GameOperate.MarkType.Attack7; // MT 是第一个小怪
                                    break;
                                case EX.PlayerRoleEnum.ST:
                                    isMyMob = markType == KodakkuAssist.Module.GameOperate.MarkType.Attack8; // ST 是第二个小怪
                                    break;
                            }
                            if (isMyMob)
                            {
                                // 自动打断 MT 或 ST
                                Task.Run(async () =>
                                {
                                    accessory.Method.SendChat($"/e 尝试打断 {markType} {_obj} <se.5>，它也许是仇恨列表中的第{rawHostileList.IndexOf(_obj.EntityId) + 1}个栏目");
                                    for (int j = 0; j < 13; j++)
                                    {
                                        try
                                        {
                                            if (_obj is IBattleChara _bc && !_bc.IsDead && _bc.IsCasting && _bc.IsCastInterruptible && !accessory.Data.MyObject.IsDead)
                                            {
                                                accessory.Method.UseAction(_obj.EntityId, InterjectActionId);
                                                accessory.Log.Debug($"自动打断 => {accessory.GetMyRole()} to {_obj}");
                                            }
                                            await Task.Delay(500); // 等待500毫秒
                                        }
                                        catch (System.Exception ex)
                                        {
                                            accessory.Log.Error($"自动打断异常 => {ex}");
                                        }
                                    }
                                });
                            }
                        }
                        await Task.Delay(10);
                    }
                    break;
            }

            // 是否为远敏玩家自动选中最近的月环小怪?





        }


        [ScriptMethod(name: "P1/P3 石化波动 安全区绘制 Quarry Swamp Safe Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.QuarrySwampActionId])]
        public void P1P3_QuarrySwampSafeZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                石化眼的安全区由四个甜甜圈扇形构成
                InnerScale = BOSS到小怪尸体的距离;
                Scale = 50;
                Radian = Atan2(小怪HitBox半径,BOSS到小怪尸体的距离) * 2
            */
            Vector3 bossPos = @event.SourcePosition;
            ulong bossId = @event.SourceId;
            // long destoryAt = (long)@event.DurationMilliseconds();
            long destoryAt = 4000;

            // 小怪的实体信息收集
            IEnumerable<IGameObject> mobs = accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BloomingAbomination);
            foreach (IGameObject mob in mobs)
            {
                float _dis = Util.DistanceByTwoPoints(bossPos, mob.Position);
                DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"{nameof(P1P3_QuarrySwampSafeZoneDraw)}{mob.EntityId}" + Guid.NewGuid().ToString();
                dp.Delay = 0;
                dp.DestoryAt = destoryAt;
                dp.Scale = new Vector2(50.0f, 50.0f);
                dp.InnerScale = new Vector2(_dis, _dis);
                dp.Owner = bossId;
                dp.TargetObject = mob.EntityId;
                dp.Radian = 2 * MathF.Atan2(mob.HitboxRadius, _dis);
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(QuarrySwampSafeZoneImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default,
                    DrawTypeEnum.Donut, dp);
            }
        }


        [ScriptMethod(name: "P1 爆炸(三连距离衰减) 危险区绘制 Explosion Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.ExplosionActionId])]
        public void P1_ExplosionDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                x 三连衰减，第一次的中心位置标记较大的危险区，第三次的中心位置标记较小的安全区
                三连衰减，根据先后顺序, 第一个着色较深, 第三个着色较浅
            */
            uint count = 0;
            lock (_lock)
            {
                count = ++ExplosionCount;
            }
            Vector3 pos = @event.EffectPosition;
            // (long, long) delay_destoryAtFirst = new(0, (long)@event.DurationMilliseconds());
            (long, long) delay_destoryAtFirst = new(0, 9000);
            float density = count switch
            {
                1 => 2.0f, // 第一次
                2 => 0.7f, // 第二次
                _ => 0.3f, // 第三次
            };
            float radius_AOE = 25.0f;
            accessory.FastDraw(DrawTypeEnum.Circle, pos, new Vector2(radius_AOE, radius_AOE), delay_destoryAtFirst,
                accessory.Data.DefaultDangerColor.WithW(density));
        }

        [ScriptMethod(name: "P1/P3 荆棘挥打(奶妈分摊) 分摊范围", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
        public void P1_奶妈分摊分摊范围(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"奶妈分摊 分摊范围";
            dp.Scale = new(6);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = @event.TargetId;
            // P3是否添加delay?
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "P1/P3 荆棘挥打(奶妈分摊) 八方站位预指", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00A1"])]
        public void P1_奶妈分摊八方站位预指(Event @event, ScriptAccessory accessory)
        {
            var tpos = @event.TargetPosition;
            var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            var bossPos = accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BruteAbombinator).FirstOrDefault()?.Position ?? Vector3.Zero;
            var drot = myindex switch
            {
                1 => 4,
                2 => 2,
                3 => 6,
                4 => 3,
                5 => 5,
                6 => 1,
                7 => 7,
                _ => 0
            };
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"奶妈分摊 八方站位预指";
            dp.Owner = @event.TargetId;
            dp.TargetPosition = bossPos;
            dp.Rotation = float.Pi + float.Pi / 4 * drot;
            dp.Scale = new(2, 8);
            // P3是否添加delay?
            dp.DestoryAt = 5200;
            dp.Color = GuideColor_GoLater.V4;
            accessory.Method.SendDraw(GuideDrawMode, DrawTypeEnum.Displacement, dp);
        }
        [ScriptMethod(name: "P1/P3 荆棘挥打(奶妈分摊) 八方站位指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:42362"])]
        public void P1_奶妈分摊八方站位指路(Event @event, ScriptAccessory accessory)
        {
            var spos = @event.SourcePosition;
            var srot = @event.SourceRotation;
            var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
            var drot = myindex switch
            {
                1 => 4,
                2 => 2,
                3 => 6,
                4 => 3,
                5 => 5,
                6 => 1,
                7 => 7,
                _ => 0
            };
            Vector3 tpos = new(spos.X + MathF.Sin(srot + float.Pi / 4 * drot) * 8, spos.Y, spos.Z + MathF.Cos(srot + float.Pi / 4 * drot) * 8);
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"奶妈分摊 八方站位";
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = tpos;
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = GuideColor_GoNow.V4;
            dp.DestoryAt = 2000;
            accessory.Method.SendDraw(GuideDrawMode, DrawTypeEnum.Displacement, dp);
        }



        [ScriptMethod(name: "P1/P3 荆棘挥打(分摊+八方) 危险区绘制 It Came From The Dirt Dangerous Zone Draw",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: [DataM7S.PulpSmashActionId])]
        public void P1P3_ItCameFromTheDirtDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                为分摊绘制脚下小钢铁
                为分摊后的分散绘制八分的扇形
                记录分摊跳跃前的面向
                记录分摊跳跃后的面向
            */

            // 画脚下小钢铁
            ulong bossId = @event.SourceId;
            float radius_centreDanger = 6.0f;
            (long Delay, long DestoryAt) delay_destoryAtCentreDanger = new(1800, 2300);
            accessory.FastDraw(DrawTypeEnum.Circle, bossId, new Vector2(radius_centreDanger, radius_centreDanger),
                 delay_destoryAtCentreDanger, accessory.Data.DefaultDangerColor.WithW(1.5f));

            // 画八方扇形
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            // dp.Name
            dp.Delay = delay_destoryAtCentreDanger.Delay;
            dp.DestoryAt = delay_destoryAtCentreDanger.DestoryAt;
            dp.Scale = new Vector2(30.0f, 30.0f);
            dp.InnerScale = new Vector2(radius_centreDanger, radius_centreDanger);
            dp.Owner = bossId;
            // dp.TargetObject = mob.EntityId;
            dp.Radian = (3.0f / 18.0f) * MathF.PI;
            dp.Color = accessory.Data.DefaultDangerColor;


            foreach (uint playerId in accessory.Data.PartyList)
            {
                dp.Name = nameof(P1P3_ItCameFromTheDirtDangerousZoneDraw) + playerId.ToString() + Guid.NewGuid().ToString();
                dp.TargetObject = playerId;
                // 过滤一下死掉的哥们
                IGameObject obj = accessory.Data.Objects.SearchById((ulong)playerId);
                if (obj is null || obj.IsDead)
                {
                    continue;
                }
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            }

        }

        [ScriptMethod(name: "P1/P3 荆棘挥打(分摊+八方) 指路绘制 It Came From The Dirt Guide Draw",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: [DataM7S.PulpSmashActionId],
            userControl: false)]
        public void P1P3_ItCameFromTheDirtGuideDraw(Event @event, ScriptAccessory accessory)
        {
            return;
            ulong bossId = @event.SourceId;
            float bossRadius = 8;
            Vector3 offSetBack = new Vector3(0, 0, bossRadius + 2);
            Vector3 offSetFront = new Vector3(0, 0, -(bossRadius + 2));
            Vector3 offSetLeft = new Vector3(-(bossRadius + 2), 0, 0);
            Vector3 offSetRight = new Vector3((bossRadius + 2), 0, 0);
            Vector3 offSetFrontLeft = new Vector3(-0.7f * (bossRadius + 2), 0, -0.7f * (bossRadius + 2));
            Vector3 offSetFrontRight = new Vector3(0.7f * (bossRadius + 2), 0, -0.7f * (bossRadius + 2));
            Vector3 offSetBackLeft = new Vector3(-0.7f * (bossRadius + 2), 0, 0.7f * (bossRadius + 2));
            Vector3 offSetBackRight = new Vector3(0.7f * (bossRadius + 2), 0, 0.7f * (bossRadius + 2));

            // Vector3 myOffset = offSetBack;
            // float myOffsetRot = MathF.Atan2(myOffset.Z, myOffset.X);
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            Vector3 myOffset = (myRole, WalkthroughType) switch
            {
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP) => offSetFront,
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP) => offSetBack,
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP) => offSetLeft,
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP) => offSetRight,
                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP) => offSetBackLeft,
                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP) => offSetBackRight,
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP) => offSetFrontLeft,
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP) => offSetFrontRight,
                _ => offSetBack // 默认值
            };

            DrawPropertiesEdit dpArrow = accessory.Data.GetDefaultDrawProperties();
            dpArrow.Name = "ItCameFromTheDirtGuideDraw 0 " + Guid.NewGuid().ToString();
            // 请微调以下两个数值以达到较优的指路效果
            dpArrow.Delay = 1650 - 1650;
            dpArrow.DestoryAt = 2200 + 1650;
            dpArrow.Scale = new(4);
            dpArrow.ScaleMode |= ScaleMode.YByDistance;
            dpArrow.Owner = bossId;
            dpArrow.TargetObject = (ulong)accessory.Data.Me;
            dpArrow.Offset = myOffset;
            dpArrow.Color = MultiDisProp.Color_GoNow.WithW(MultiDisProp.Color_GoNow.W + 1);
            accessory.Method.SendDraw(GuideDrawMode, DrawTypeEnum.Line, dpArrow);
            dpArrow.Color = MultiDisProp.Color_GoNow.WithW(MultiDisProp.Color_GoNow.W + 4);
            dpArrow.ScaleMode = ScaleMode.None;
            dpArrow.Scale = new(0.05f, 3);
            dpArrow.Rotation = 0.4f;
            dpArrow.Name = "ItCameFromTheDirtGuideDraw 1 " + Guid.NewGuid().ToString();
            accessory.Method.SendDraw(GuideDrawMode, DrawTypeEnum.Rect, dpArrow);
            dpArrow.Rotation = -0.4f;
            dpArrow.Name = "ItCameFromTheDirtGuideDraw 2 " + Guid.NewGuid().ToString();
            accessory.Method.SendDraw(GuideDrawMode, DrawTypeEnum.Rect, dpArrow);

        }

        [ScriptMethod(name: "P1 新式超豪华野蛮大乱击(转场AOE) 安全区绘制 Neo Bombarian Special Safe Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.NeoBombarianSpecialActionId])]
        public void P1_NeoBombarianSpecialSafeZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                P1转场击退绘制安全区范围
            */
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = nameof(P1_NeoBombarianSpecialSafeZoneDraw) + Guid.NewGuid().ToString();
            dp.Delay = 500; // 由于有时候BOSS会有个转向的动作, 添加一点延迟
            // dp.DestoryAt = (long)@event.DurationMilliseconds() - dp.Delay;
            dp.DestoryAt = 8000 - dp.Delay;
            dp.Scale = new Vector2(8, 30.0f);
            dp.Owner = @event.SourceId;
            dp.Offset = new Vector3(0, 0, -11.5f);
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }




        [ScriptMethod(name: "P2 野蛮电火花(GA-100) 危险区绘制 Abominable Blink Dangerous Zone Draw",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: [DataM7S.AbominableBlinkIconId])]
        public void P2_AbominableBlinkDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            accessory.FastDraw(DrawTypeEnum.Circle, @event.TargetId, new Vector2(25, 25), new(0, 6480), false);
            // 去到BOSS自己的右前方
            Vector3 bossPos = DataM7S.P2_FieldCenter;
            IGameObject bossObj = accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BruteAbombinator).FirstOrDefault();
            if (bossObj is not null)
            {
                bossPos = bossObj.Position;
            }
            bool isBossNearWall = bossPos.Z < DataM7S.P2_FieldCenter.Z;
            Vector3 myPos = isBossNearWall ? new Vector3(12, 0, -24.5f) + DataM7S.P2_FieldCenter : new Vector3(-12, 0, 24.5f) + DataM7S.P2_FieldCenter;
            if ((ulong)accessory.Data.Me == @event.TargetId)
            {
                // 我是目标
                EX.DisplacementContainer myEndPos = new(myPos, 0, 5000);
                accessory.MultiDisDraw(new List<EX.DisplacementContainer> { myEndPos }, MultiDisProp);
            }
        }

        [ScriptMethod(name: "P2 野蛮电火花(GA-100) 坦克职能自动支援减 Abominable Blink Tank Support",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: [DataM7S.AbominableBlinkIconId])]
        public void P2_AbominableBlinkTankSupport(Event @event, ScriptAccessory accessory)
        {
            if (!AutoTankSupportEnable) return;
            uint tarId = (uint)@event.TargetId;
            IPlayerCharacter? myChara = accessory.Data.MyObject;
            if (myChara is null || !myChara.IsTank() || myChara.IsDead) return;
            List<IBattleChara> thornyDeathmatchPlayers = new List<IBattleChara>();
            try
            {
                foreach (uint id in accessory.Data.PartyList)
                {
                    IGameObject obj = accessory.Data.Objects.SearchById((ulong)id);
                    if (obj is IBattleChara bc && !bc.IsDead && (
                        bc.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                        || bc.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                        || bc.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                        || bc.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV)
                        || bc.HasStatus((uint)DataM7S.SID.ThornsOfDeathI)
                        || bc.HasStatus((uint)DataM7S.SID.ThornsOfDeathII)
                        || bc.HasStatus((uint)DataM7S.SID.ThornsOfDeathIII)
                        || bc.HasStatus((uint)DataM7S.SID.ThornsOfDeathIV)
                    ))
                    {
                        thornyDeathmatchPlayers.Add(bc);
                    }
                }
            }
            catch (System.Exception ex)
            {
                accessory.Log.Error($"获取荆棘缠绕玩家异常 => {ex}");
            }
            if (thornyDeathmatchPlayers.Count == 0) return;
            uint toSupportId = 0;
            if (thornyDeathmatchPlayers.Count == 1)
            {
                // 只有一个荆棘缠绕玩家, 一般是一仇, 给他支援减即可
                if (tarId != accessory.Data.Me) toSupportId = tarId;
            }
            else
            {
                // 查找短边连线的玩家是否离BOSS较近。
                IGameObject bossObj = accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BruteAbombinator).FirstOrDefault();
                Vector3 bossPos = DataM7S.P2_FieldCenter;
                if (bossObj is not null)
                {
                    bossPos = bossObj.Position;
                }
                bool isBossNearWall = bossPos.Z < DataM7S.P2_FieldCenter.Z;
                if (isBossNearWall)
                {
                    // BOSS靠近短边，给短边处的非坦克玩家支援减
                    IGameObject? toSupportObj = thornyDeathmatchPlayers.Where(bc => !bc.IsTank())
                                                                       .OrderBy(bc => Util.DistanceByTwoPoints(bc.Position, new Vector3(0, 0, -35) + DataM7S.P2_FieldCenter))
                                                                       .FirstOrDefault();
                    if (toSupportObj is not null)
                    {
                        toSupportId = toSupportObj.EntityId;
                        accessory.Log.Debug($"P2 AbominableBlinkTankSupport: 给短边连线玩家 {toSupportId} 支援减");
                    }
                }
                else
                {
                    // BOSS远离短边，给目标T支援减
                    if (tarId != accessory.Data.Me) toSupportId = tarId;
                }
            }
            if (toSupportId == 0) return;
            // 给目标T支援减
            uint mySupportActionId = accessory.MyJob() switch
            {
                EX.Job.WAR => 16464, // 绿血气
                EX.Job.PLD => 7382, // 干预
                EX.Job.DRK => 7393, // 黑盾
                EX.Job.GNB => 25758, // 刚玉
                _ => 0,
            };
            if (mySupportActionId == 0) return;
            accessory.Log.Debug($"P2 AbominableBlinkTankSupport: 自动给 {toSupportId} 支援减");
            Task.Run(async () =>
            {
                await Task.Delay(1800); // 等待1500毫秒
                IGameObject? toSupportObj = accessory.Data.Objects.SearchById((ulong)toSupportId);
                accessory.Method.SendChat($"/e 尝试给 {toSupportObj} 支援减 <se.5>");
                for (int j = 0; j < 6; j++)
                {
                    try
                    {
                        if (toSupportObj is IBattleChara _bc && !_bc.IsDead && !accessory.Data.MyObject.IsDead)
                        {
                            accessory.Method.UseAction(toSupportObj.EntityId, mySupportActionId);
                            accessory.Log.Debug($"自动支援减 => {accessory.MyJob()} to {toSupportObj}");
                        }
                        await Task.Delay(500); // 等待500毫秒
                    }
                    catch (System.Exception ex)
                    {
                        accessory.Log.Error($"自动支援减异常 => {ex}");
                    }
                }
            });
        }
        [ScriptMethod(name: "P2/P3 野蛮横扫(跳跃 + 钢铁/月环) 计数",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3],
            userControl: false)]
        public void P2P3_BrutishSwingCastingCountCalc(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y > -100)
            {
                P2_BrutishSwingCastedCount++;
                P3_BrutishSwingCastedCount = 0;
            }
            else
            {
                P3_BrutishSwingCastedCount++;
                P2_BrutishSwingCastedCount = 0;
            }

        }

        [ScriptMethod(name: "P2/P3 野蛮横扫(跳跃 + 钢铁/月环) 危险区绘制 Brutish Swing Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3])]
        public void P2P3_BrutishSwingDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                拔武器+跳跃 在目标地点绘制危险区
            */
            uint actionId = @event.ActionId;
            ulong bossId = @event.SourceId;
            Vector3 effectPos = @event.EffectPosition;
            long destoryAt = @event.SourcePosition.Y < -100 ? 5700 : 7600;
            (long, long) delay_destoryAt = new(0, destoryAt);
            float radius_Stick = 25.0f;
            float radius_Machete = 22.0f;

            if (P3_BrutishSwingCastedCount >= 2 && actionId == (uint)DataM7S.AID.BrutishSwingMachete_P3)
            {
                delay_destoryAt = new(1160, destoryAt - 1160);
            }
            else if (P2_BrutishSwingCastedCount >= 1 && actionId == (uint)DataM7S.AID.BrutishSwingMachete_P2)
            {
                delay_destoryAt = new(2460, destoryAt - 2460);
            }
            // if (LashingLariatCastingCount > 1)
            // {
            //     // P3第二组的双武器跳跃，要放置冰花, 绘图稍微延后
            //     // 月环延后, 钢铁不延
            //     if (actionId == (uint)DataM7S.AID.BrutishSwingMachete_P3)
            //     {
            //         delay_destoryAt = new(1160, destoryAt - 1160);
            //     }
            // }
            // else if (StrangeSeedsCount > 0)
            // {
            //     // 连冰花后的武器跳跃也需要延后，月环延后, 钢铁不延
            //     // ↑我去,GA-100后的三穿一的月环也得延后，否则会看不清场地上的安全区
            //     if (actionId == (uint)DataM7S.AID.BrutishSwingMachete_P2)
            //     {
            //         delay_destoryAt = new(2460, destoryAt - 2460);
            //     }
            // }
            // else if (IsAbominableBlinkCasting)
            // {
            //     IsAbominableBlinkCasting = false;
            //     if (actionId == (uint)DataM7S.AID.BrutishSwingMachete_P2)
            //     {
            //         delay_destoryAt = new(2460, destoryAt - 2460);
            //     }
            // }
            switch (actionId)
            {
                case (uint)DataM7S.AID.BrutishSwingStick_P2:
                case (uint)DataM7S.AID.BrutishSwingStick_P3:
                    accessory.FastDraw(DrawTypeEnum.Circle, effectPos, new Vector2(radius_Stick, radius_Stick), delay_destoryAt, false);
                    break;
                case (uint)DataM7S.AID.BrutishSwingMachete_P2:
                case (uint)DataM7S.AID.BrutishSwingMachete_P3:
                    accessory.FastDraw(DrawTypeEnum.Donut, effectPos, new Vector2(radius_Machete * 4, radius_Machete), delay_destoryAt, false);
                    break;
            }
        }

        [ScriptMethod(name: "P2 野蛮横扫(跳跃 + 钢铁/月环) 指路绘制 Brutish Swing Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3])]
        public void P2_BrutishSwingGuideDraw(Event @event, ScriptAccessory accessory)
        {

            Vector3 tarPos = @event.EffectPosition;
            if (tarPos.Y < -100) return;
            /*
                有三种情况，在长边墙壁，靠近短边墙壁
                           在长边墙壁，远离短边墙壁
                           在短边墙壁
            */
            const string JumpToFarWall = "Jump to Far Wall";
            const string JumpToNearWall = "Jump to Near Wall";
            const string JumpToShortWall = "Jump to Short Wall";
            string bossJumpType = JumpToShortWall;
            if (MathF.Abs(tarPos.X - DataM7S.P2_FieldCenter.X) < 0.5f)
            {
                // 在短边墙壁
                bossJumpType = JumpToShortWall;
            }
            else if (tarPos.Z < DataM7S.P2_FieldCenter.Z)
            {
                // 在长边墙壁，靠近短边墙壁
                bossJumpType = JumpToNearWall;
            }
            else
            {
                // 在长边墙壁，远离短边墙壁
                bossJumpType = JumpToFarWall;
            }
            bool isStick = @event.ActionId == (uint)DataM7S.AID.BrutishSwingStick_P2;
            Vector3 startPos = DataM7S.P2_FieldCenter;

            EX.PlayerRoleEnum myRole = accessory.GetMyRole();

            if (isStick)
            {
                // 情况较多
                startPos = (myRole, bossJumpType, WalkthroughType) switch
                {
                    (EX.PlayerRoleEnum.MT, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(3, 0, -22.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.MT, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-3, 0, 22.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.MT, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-11, 0, -16.5f) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.ST, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(3, 0, -2.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.ST, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-3, 0, 2.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.ST, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(11, 0, -16.5f) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.H1, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, 3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H1, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, -3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H1, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-11, 0, -10) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.H2, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, -3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H2, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, 3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H2, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(11, 0, -10) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.D1, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(3, 0, -22.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D1, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-3, 0, 22.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D1, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-11, 0, -16.5f) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.D2, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(3, 0, -2.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D2, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-3, 0, 2.5f) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D2, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(11, 0, -16.5f) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.D3, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-10, 0, 3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D3, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(10, 0, -3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D3, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-11, 0, 3) + DataM7S.P2_FieldCenter,

                    (EX.PlayerRoleEnum.D4, JumpToNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-10, 0, -3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D4, JumpToFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(10, 0, 3) + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D4, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(11, 0, 3) + DataM7S.P2_FieldCenter,

                    _ => startPos,
                };
            }
            else
            {
                // 情况较少,直接到目标圈内即可
                Vector3 inCircleLeft = new Vector3(8, 0, -22);
                Vector3 inCircleRight = new Vector3(8, 0, -3);
                if (bossJumpType == JumpToFarWall)
                {
                    // 在长边墙壁，远离短边墙壁
                    inCircleLeft = new Vector3(-inCircleLeft.X, 0, -inCircleLeft.Z);
                    inCircleRight = new Vector3(-inCircleRight.X, 0, -inCircleRight.Z);
                }
                else if (bossJumpType == JumpToShortWall)
                {
                    // 在短边墙壁
                    inCircleLeft = new Vector3(-11, 0, -22);
                    inCircleRight = new Vector3(11, 0, -22);
                }
                startPos = (myRole, bossJumpType, WalkthroughType) switch
                {
                    (EX.PlayerRoleEnum.MT, _, WalkthroughEnum.MMW_SPJP) => inCircleLeft + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.ST, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H1, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => inCircleLeft + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H1, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.H2, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D1, _, WalkthroughEnum.MMW_SPJP) => inCircleLeft + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D2, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D3, JumpToShortWall, WalkthroughEnum.MMW_SPJP) => inCircleLeft + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D3, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    (EX.PlayerRoleEnum.D4, _, WalkthroughEnum.MMW_SPJP) => inCircleRight + DataM7S.P2_FieldCenter,
                    _ => startPos,
                };
            }



            /*
               有三种情况，在长边墙壁，靠近短边墙壁
                          在长边墙壁，远离短边墙壁
                          在短边墙壁
           */
            // Vector3 bossPos = @event.SourcePosition;
            const string OnNearWall = "On Near Wall";
            const string OnFarWall = "On Far Wall";
            const string OnShortWall = "On Short Wall";
            string bossWallType = OnShortWall;
            if (MathF.Abs(tarPos.X - DataM7S.P2_FieldCenter.X) < 0.5f)
            {
                // 在短边墙壁
                bossWallType = OnShortWall;
            }
            else if (tarPos.Z < DataM7S.P2_FieldCenter.Z)
            {
                // 在长边墙壁，靠近短边墙壁
                bossWallType = OnNearWall;
            }
            else
            {
                // 在长边墙壁，远离短边墙壁
                bossWallType = OnFarWall;
            }
            Vector3 endPos = DataM7S.P2_FieldCenter;
            // EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            endPos = (myRole, bossWallType, WalkthroughType) switch
            {
                (EX.PlayerRoleEnum.MT, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(5, 0, -20) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.MT, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-5, 0, 20) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.MT, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-7.5f, 0, -16.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.ST, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(7, 0, -5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.ST, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-7, 0, 5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.ST, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(7.5f, 0, -16.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.H1, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, 10) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.H1, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(0, 0, -10) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.H1, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-12, 0, -8.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.H2, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-2, 0, -5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.H2, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(2, 0, 5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.H2, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(12, 0, -8.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.D1, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(12, 0, -24.5f) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D1, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-12, 0, 24.5f) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D1, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-12, 0, -24.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.D2, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(12, 0, 3) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D2, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-12, 0, -3) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D2, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(12, 0, -24.5f) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.D3, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-10, 0, 10) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D3, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(10, 0, -10) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D3, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-7.5f, 0, 2) + DataM7S.P2_FieldCenter,

                (EX.PlayerRoleEnum.D4, OnNearWall, WalkthroughEnum.MMW_SPJP) => new Vector3(-12, 0, -5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D4, OnFarWall, WalkthroughEnum.MMW_SPJP) => new Vector3(12, 0, 5) + DataM7S.P2_FieldCenter,
                (EX.PlayerRoleEnum.D4, OnShortWall, WalkthroughEnum.MMW_SPJP) => new Vector3(7.5f, 0, 2) + DataM7S.P2_FieldCenter,

                _ => endPos,
            };

            if (P2GlowerPowerGuideDrawEnabled)
            {
                accessory.MultiDisDraw(new List<EX.DisplacementContainer>
                {
                    new(startPos, 0, 8000),
                    new(endPos, 0, 5700)
                }, MultiDisProp);
            }
            else
            {
                accessory.MultiDisDraw(new List<EX.DisplacementContainer>
                {
                    new(startPos, 0, 8000),
                }, MultiDisProp);
            }

        }



        [ScriptMethod(name: "P2 野蛮怒视 危险区绘制 P2 Glower Power Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.GlowerPowerActionId])]
        public void P2_GlowerPowerDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                嘴炮 绘制直线+跟随人物的分散
            */
            // long destoryAt = @event.DurationMilliseconds();
            long destoryAt = 4000;
            (long, long) delay_destoryAt = new(0, destoryAt);
            // (long, long) delay_destoryAtRect = new (0, destoryAt - 1300);
            float radius_AOE = 6.0f;

            accessory.FastDraw(DrawTypeEnum.Rect, @event.SourceId, new Vector2(14.0f, 65.0f), delay_destoryAt, false);
            foreach (uint playerId in accessory.Data.PartyList)
            {
                ulong _id = (ulong)playerId;
                // 过滤一下死掉的哥们
                IGameObject obj = accessory.Data.Objects.SearchById(_id);
                if (obj is null || obj.IsDead)
                {
                    continue;
                }
                accessory.FastDraw(DrawTypeEnum.Circle, _id, new Vector2(radius_AOE, radius_AOE), delay_destoryAt, false);
            }

        }

        [ScriptMethod(name: "P2 荆棘生死战·楼体 指路绘制 P2 DemolitionDeathmatch Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.DemolitionDeathmatchActionId])]
        public void P2_DemolitionDeathmatchGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 bossPos = @event.SourcePosition;
            Vector3 bossToC = DataM7S.P2_FieldCenter - bossPos;

            EX.PlayerRoleEnum myRole = accessory.GetMyRole();

            Vector3 myEndPos = (myRole, WalkthroughType) switch
            {
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP) => DataM7S.P2_FieldCenter + new Vector3(0, 0, -24),
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP) => DataM7S.P2_FieldCenter + new Vector3(0.35f * bossToC.X, 0, 0.35f * bossToC.Z),
                _ => DataM7S.P2_FieldCenter
            };
            if (myEndPos == DataM7S.P2_FieldCenter) return; // 过滤掉不在场地内的情况
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> { new(myEndPos, 0, 4500) }, MultiDisProp);
        }

        [ScriptMethod(name: "P2 点名冰花(两两冰花) 预站位 Strange Seeds Pre Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.StrangeSeedsVisualActionId_P2])]
        public void P2_StrangeSeedsPreGuideDraw(Event @event, ScriptAccessory accessory)
        {
            bool isBossNearWall = @event.SourcePosition.Z < DataM7S.P2_FieldCenter.Z;
            bool isFixedStrangeSeeds = P2StrangeSeedsFixed;
            bool isHintFull = !P2StrangeSeedsSimpleStyle;
            (long Delay, long DestoryAt) delay_destoryAt = new(0, 5500);
            if (isFixedStrangeSeeds)
            {
                // BOSS靠近墙壁的情况
                Vector3 fixedMT = new Vector3(9.7f, 0, -12.5f);
                Vector3 fixedST = new Vector3(12, 0, -24.5f);
                Vector3 fixedD1 = new Vector3(4.2f, 0, -10);
                Vector3 fixedD2 = new Vector3(7.2f, 0, -2.8f);
                Vector3 fixedH1 = new Vector3(0.9f, 0, 7.2f);
                Vector3 fixedH2 = new Vector3(-1.3f, 0, 13.8f);
                Vector3 fixedD3 = new Vector3(-4.8f, 0, -19.7f);
                Vector3 fixedD4 = new Vector3(-9.2f, 0, 9.7f);

                if (!isBossNearWall)
                {
                    // BOSS远离墙壁的情况
                    fixedMT = new Vector3(-fixedMT.X, 0, -fixedMT.Z);
                    fixedST = new Vector3(-fixedST.X, 0, -fixedST.Z);
                    fixedD1 = new Vector3(-fixedD1.X, 0, -fixedD1.Z);
                    fixedD2 = new Vector3(-fixedD2.X, 0, -fixedD2.Z);
                    fixedH1 = new Vector3(-fixedH1.X, 0, -fixedH1.Z);
                    fixedH2 = new Vector3(-fixedH2.X, 0, -fixedH2.Z);
                    fixedD3 = new Vector3(4.6f, 0, fixedD3.Z);
                    fixedD4 = new Vector3(-fixedD4.X, 0, -fixedD4.Z);
                }
                Vector2 _size = new Vector2(0.5f, 0.5f);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedMT + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedST + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedH1 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedH2 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD1 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD2 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD3 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD4 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);


                return;
            }
            else
            {
                Vector3 oddForMeleeMMW = new Vector3(12, 0, -24.5f);
                Vector3 evenForMeleeMMW = new Vector3(3.4f, 0, -21);
                Vector3 oddForTHMMW = new Vector3(12, 0, 0);
                Vector3 evenForTHMMW = new Vector3(3.4f, 0, -3.4f);
                Vector3 oddForD4MMW = new Vector3(-12, 0, 0);
                Vector3 evenForD4MMW = new Vector3(-7.2f, 0, 14);
                Vector3 oddForD3MMW = new Vector3(-12, 0, -24.5f);
                Vector3 evenForD3MMW = new Vector3(-3.4f, 0, -21);
                Vector3 safePos1MMW = new Vector3(6.5f, 0, -12.5f);
                Vector3 safePos2MMW = new Vector3(7.8f, 0, 10);
                if (!isBossNearWall)
                {
                    // boss所在处没有相邻墙壁, 转180度
                    oddForMeleeMMW = new Vector3(-oddForMeleeMMW.X, 0, -oddForMeleeMMW.Z);
                    evenForMeleeMMW = new Vector3(-evenForMeleeMMW.X, 0, -evenForMeleeMMW.Z);
                    oddForTHMMW = new Vector3(-oddForTHMMW.X, 0, -oddForTHMMW.Z);
                    evenForTHMMW = new Vector3(-evenForTHMMW.X, 0, -evenForTHMMW.Z);
                    oddForD4MMW = new Vector3(-oddForD4MMW.X, 0, -oddForD4MMW.Z);
                    evenForD4MMW = new Vector3(-evenForD4MMW.X, 0, -evenForD4MMW.Z);
                    safePos1MMW = new Vector3(-safePos1MMW.X, 0, -safePos1MMW.Z);
                    safePos2MMW = new Vector3(-safePos2MMW.X, 0, -safePos2MMW.Z);
                }
                else
                {
                    // boss与短边相邻,D3的偶数轮次和近战共用一个点位
                    evenForD3MMW = evenForMeleeMMW;
                }

                if (WalkthroughType == WalkthroughEnum.MMW_SPJP)
                {

                    Vector2 size = new Vector2(0.6f, 0.3f);
                    delay_destoryAt.DestoryAt += 1000; // 多画一秒钟
                    accessory.FastDraw(DrawTypeEnum.Circle, safePos1MMW + DataM7S.P2_FieldCenter, new Vector2(2.0f, 2.0f), delay_destoryAt, accessory.Data.DefaultSafeColor.WithW(0.25f), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Circle, safePos2MMW + DataM7S.P2_FieldCenter, new Vector2(2.0f, 2.0f), delay_destoryAt, accessory.Data.DefaultSafeColor.WithW(0.25f), GuideDrawMode);

                    // 只画自己的职能
                    switch (accessory.GetMyRole())
                    {
                        case EX.PlayerRoleEnum.MT:
                        case EX.PlayerRoleEnum.ST:
                        case EX.PlayerRoleEnum.H1:
                        case EX.PlayerRoleEnum.H2:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D1:
                        case EX.PlayerRoleEnum.D2:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D3:
                            // 如果是BOSS靠近短边, D3靠近场中的那个点，和近战公用
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D4:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                    }

                }
            }
        }

        [ScriptMethod(name: "P2 点名冰花(两两冰花) 轮次着色 Strange Seeds Counts Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.StrangeSeedsActionId])]
        // suppress : 1000)]
        public void P2_StrangeSeedsCountsDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                冰花点名时，通过着色提示奇数次还是偶数次
                由于有可能点名自己的那个被 suppress ,不使用该特性
            */
            if (@event.SourcePosition.Y < -100) return; // 过滤掉不在场地内的事件
            uint count = 0;
            lock (_lock)
            {
                count = StrangeSeedsCount++;
            }
            ulong tarId = @event.TargetId;
            // (long Delay, long DestoryAt) delay_destoryAt = new(0, (long)@event.DurationMilliseconds());
            (long Delay, long DestoryAt) delay_destoryAt = new(0, 5000);
            bool isOdd = count % 4 == 0 || count % 4 == 1;
            Vector4 color = isOdd ? StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity) : StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity).WithW(P2StrangeSeedsColorDensity);
            accessory.Log.Debug($"Strange Seeds Counts Draw : count => {count / 2 + 1}");

            bool isBossNearWall = @event.SourcePosition.Z < DataM7S.P2_FieldCenter.Z;
            bool isFixedStrangeSeeds = P2StrangeSeedsFixed;
            bool isHintFull = !P2StrangeSeedsSimpleStyle;
            if (isFixedStrangeSeeds)
            {
                // BOSS靠近墙壁的情况
                Vector3 fixedMT = new Vector3(9.7f, 0, -12.5f);
                Vector3 fixedST = new Vector3(12, 0, -24.5f);
                Vector3 fixedD1 = new Vector3(4.2f, 0, -10);
                Vector3 fixedD2 = new Vector3(7.2f, 0, -2.8f);
                Vector3 fixedH1 = new Vector3(0.9f, 0, 7.2f);
                Vector3 fixedH2 = new Vector3(-1.3f, 0, 13.8f);
                Vector3 fixedD3 = new Vector3(-4.8f, 0, -19.7f);
                Vector3 fixedD4 = new Vector3(-9.2f, 0, 9.7f);

                if (!isBossNearWall)
                {
                    // BOSS远离墙壁的情况
                    fixedMT = new Vector3(-fixedMT.X, 0, -fixedMT.Z);
                    fixedST = new Vector3(-fixedST.X, 0, -fixedST.Z);
                    fixedD1 = new Vector3(-fixedD1.X, 0, -fixedD1.Z);
                    fixedD2 = new Vector3(-fixedD2.X, 0, -fixedD2.Z);
                    fixedH1 = new Vector3(-fixedH1.X, 0, -fixedH1.Z);
                    fixedH2 = new Vector3(-fixedH2.X, 0, -fixedH2.Z);
                    fixedD3 = new Vector3(4.6f, 0, fixedD3.Z);
                    fixedD4 = new Vector3(-fixedD4.X, 0, -fixedD4.Z);
                }
                Vector2 _size = new Vector2(0.5f, 0.5f);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedMT + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedST + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedH1 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedH2 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD1 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD2 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD3 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, fixedD4 + DataM7S.P2_FieldCenter, _size, delay_destoryAt, true, GuideDrawMode);


                return;
            }
            if (tarId == accessory.Data.Me)
            {
                accessory.FastDraw(DrawTypeEnum.Circle, tarId, new Vector2(2.0f, 2.0f), delay_destoryAt, color, GuideDrawMode);
            }
            else
            {
                // 冰花虽然没有点我，但是依然给一个奇偶提示
                // 如果我不是T,且身上有连线buff
                if (accessory.Data.MyObject is not null
                    && !P2StrangeSeedsSimpleStyle
                    && !accessory.Data.MyObject.IsTank()
                    && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                    || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                    || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                    || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV)))
                {
                    color = !isOdd ? StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity) : StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity);
                    accessory.FastDraw(DrawTypeEnum.Circle,
                        accessory.Data.Me, new Vector2(1.15f),
                        delay_destoryAt,
                        color.WithW(color.W * 0.3f), GuideDrawMode);
                }
            }


            Vector3 oddForMeleeMMW = new Vector3(12, 0, -24.5f);
            Vector3 evenForMeleeMMW = new Vector3(3.4f, 0, -21);
            Vector3 oddForTHMMW = new Vector3(12, 0, 0);
            Vector3 evenForTHMMW = new Vector3(3.4f, 0, -3.4f);
            Vector3 oddForD4MMW = new Vector3(-12, 0, 0);
            Vector3 evenForD4MMW = new Vector3(-7.2f, 0, 14);
            Vector3 oddForD3MMW = new Vector3(-12, 0, -24.5f);
            Vector3 evenForD3MMW = new Vector3(-3.4f, 0, -21);
            Vector3 safePos1MMW = new Vector3(6.5f, 0, -12.5f);
            Vector3 safePos2MMW = new Vector3(7.8f, 0, 10);

            if (!isBossNearWall)
            {
                // boss所在处没有相邻墙壁, 转180度
                oddForMeleeMMW = new Vector3(-oddForMeleeMMW.X, 0, -oddForMeleeMMW.Z);
                evenForMeleeMMW = new Vector3(-evenForMeleeMMW.X, 0, -evenForMeleeMMW.Z);
                oddForTHMMW = new Vector3(-oddForTHMMW.X, 0, -oddForTHMMW.Z);
                evenForTHMMW = new Vector3(-evenForTHMMW.X, 0, -evenForTHMMW.Z);
                oddForD4MMW = new Vector3(-oddForD4MMW.X, 0, -oddForD4MMW.Z);
                evenForD4MMW = new Vector3(-evenForD4MMW.X, 0, -evenForD4MMW.Z);
                safePos1MMW = new Vector3(-safePos1MMW.X, 0, -safePos1MMW.Z);
                safePos2MMW = new Vector3(-safePos2MMW.X, 0, -safePos2MMW.Z);
            }
            else
            {
                // boss与短边相邻,D3的偶数轮次和近战共用一个点位
                evenForD3MMW = evenForMeleeMMW;
            }

            // 画一下奇偶指示点
            if (WalkthroughType == WalkthroughEnum.MMW_SPJP)
            {
                // (long, long) delay_destoryAt = new(0, 5000);
                Vector2 size = new Vector2(0.6f, 0.3f);
                delay_destoryAt.DestoryAt += 1000; // 多画一秒钟
                accessory.FastDraw(DrawTypeEnum.Circle, safePos1MMW + DataM7S.P2_FieldCenter, new Vector2(2.0f, 2.0f), delay_destoryAt, accessory.Data.DefaultSafeColor.WithW(0.25f), GuideDrawMode);
                accessory.FastDraw(DrawTypeEnum.Circle, safePos2MMW + DataM7S.P2_FieldCenter, new Vector2(2.0f, 2.0f), delay_destoryAt, accessory.Data.DefaultSafeColor.WithW(0.25f), GuideDrawMode);
                if (isHintFull)
                {
                    // 如果要全画
                    // 画出所有的奇偶指示点
                    accessory.FastDraw(DrawTypeEnum.Donut, oddForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, evenForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, oddForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, evenForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, oddForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, evenForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, oddForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                    accessory.FastDraw(DrawTypeEnum.Donut, evenForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                }
                else
                { // 只画自己的职能
                    switch (accessory.GetMyRole())
                    {
                        case EX.PlayerRoleEnum.MT:
                        case EX.PlayerRoleEnum.ST:
                        case EX.PlayerRoleEnum.H1:
                        case EX.PlayerRoleEnum.H2:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForTHMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D1:
                        case EX.PlayerRoleEnum.D2:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForMeleeMMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D3:
                            // 如果是BOSS靠近短边, D3靠近场中的那个点，和近战公用
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForD3MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                        case EX.PlayerRoleEnum.D4:
                            accessory.FastDraw(DrawTypeEnum.Donut, oddForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountOdd.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            accessory.FastDraw(DrawTypeEnum.Donut, evenForD4MMW + DataM7S.P2_FieldCenter, size, delay_destoryAt, StrangeSeedsCountEven.V4.WithW(P2StrangeSeedsColorDensity), GuideDrawMode);
                            break;
                    }
                }
            }
        }
        [ScriptMethod(name: "P2 种弹重击(分摊冰花) 指路绘制 Killer Seeds Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.KillerSeedsActionId],
            suppress: 1000)]
        public void P2_KillerSeedsGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 bossPos = @event.SourcePosition;
            if (bossPos.Y < -100) return; // 只在P2阶段绘制


            bool isBossNearWall = bossPos.Z < DataM7S.P2_FieldCenter.Z;
            Vector3 groupMT = new Vector3(12, 0, -24.5f);
            Vector3 groupST = new Vector3(12, 0, 0);
            Vector3 groupH1 = new Vector3(-12, 0, -24.5f);
            Vector3 groupH2 = new Vector3(-12, 0, 0);

            if (!isBossNearWall)
            {
                // boss所在处没有相邻墙壁, 转180度
                groupMT = new Vector3(-groupMT.X, 0, -groupMT.Z);
                groupST = new Vector3(-groupST.X, 0, -groupST.Z);
                groupH1 = new Vector3(groupH1.X, 0, groupH1.Z);
                groupH2 = new Vector3(-groupH2.X, 0, -groupH2.Z);
            }


            EX.PlayerRoleEnum myRole = accessory.GetMyRole();

            Vector3 myPos = (WalkthroughType, myRole) switch
            {
                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.MT) => groupMT,
                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.D1) => groupMT,

                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.ST) => groupST,
                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.D2) => groupST,

                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.H1) => groupH1,
                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.D3) => groupH1,

                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.H2) => groupH2,
                (WalkthroughEnum.MMW_SPJP, EX.PlayerRoleEnum.D4) => groupH2,
                _ => Vector3.Zero
            };
            // 绘制指路
            EX.DisplacementContainer myEndPos = new(myPos + DataM7S.P2_FieldCenter, 0, 4700);
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> { myEndPos }, MultiDisProp);

        }

        [ScriptMethod(name: "P3 Stoneringer Id获取 Stoneringer Action Id",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.P3_StoneringerActionId], userControl: false)]
        public void P3_StoneringerActionId(Event @event, ScriptAccessory accessory)
        {
            P3_StoneringerId = @event.ActionId;
            SinisterSeedTargets.Clear(); // 清空冰花目标
        }

        [ScriptMethod(name: "P3 藤蔓碎颈臂 危险区预绘制 Lashing Lariat Dangerous Zone Pre Draw ",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3])]
        public void P3_LashingLariatDangerousZonePreDraw(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y > -100) return; // 只在P3绘制
            if (P3_BrutishSwingCastedCount == 0 || P3_BrutishSwingCastedCount == 2)
            {
                Vector3 effectPos = @event.EffectPosition;
                uint actionId = @event.ActionId;
                // 判断出偏移方向，由于不能使用Owner属性，必须手动计算坐标
                // 42400 左手刀，右手棒
                // 42401 左手棒，右手刀
                // 42403 棒跳
                // 42405 刀跳

                // 假设跳到正北方向的墙面
                Vector3 offset = (P3_StoneringerId, actionId) switch
                {
                    ((uint)DataM7S.AID.Stoneringer2Stoneringers_LStick, (uint)DataM7S.AID.BrutishSwingStick_P3)
                        => new Vector3(9, 0, 0), // 左手棒 + 棒跳
                    ((uint)DataM7S.AID.Stoneringer2Stoneringers_LStick, (uint)DataM7S.AID.BrutishSwingMachete_P3)
                        => new Vector3(-9, 0, 0), // 左手棒 + 刀跳
                    ((uint)DataM7S.AID.Stoneringer2Stoneringers_RStick, (uint)DataM7S.AID.BrutishSwingStick_P3)
                        => new Vector3(-9, 0, 0), // 右手棒 + 棒跳
                    ((uint)DataM7S.AID.Stoneringer2Stoneringers_RStick, (uint)DataM7S.AID.BrutishSwingMachete_P3)
                        => new Vector3(9, 0, 0), // 右手棒 + 刀跳
                    _ => Vector3.Zero
                };
                Vector3 startPos = new Vector3(0, 0, -35) + DataM7S.P3_FieldCenter + offset;
                Vector3 tarPos = DataM7S.P3_FieldCenter + offset;
                float _rot = MathF.Atan2(effectPos.Z - DataM7S.P3_FieldCenter.Z, effectPos.X - DataM7S.P3_FieldCenter.X);
                float modRot = _rot + MathF.PI / 2;
                startPos = Util.RotatePointInFFXIV(startPos, DataM7S.P3_FieldCenter, modRot);
                tarPos = Util.RotatePointInFFXIV(tarPos, DataM7S.P3_FieldCenter, modRot);

                DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = nameof(P3_LashingLariatDangerousZonePreDraw) + Guid.NewGuid().ToString();
                dp.Delay = 6500;
                dp.DestoryAt = 4000;
                dp.Scale = new Vector2(32.0f, 70.0f);
                dp.Position = startPos;
                dp.TargetPosition = tarPos;
                dp.Color = accessory.Data.DefaultDangerColor.WithW(0.7f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);


            } 
            

            
        }

        [ScriptMethod(name: "P3 藤蔓碎颈臂 危险区绘制 Lashing Lariat Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.LashingLariatActionId])]
        public void P3_LashingLariatDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            /*
                P3 冲锋，绘制危险区
            */

            uint actionId = @event.ActionId;
            ulong bossId = @event.SourceId;
            // Vector3 tarPos = @event.EffectPosition;
            // long destoryAt = (long)@event.DurationMilliseconds();
            long destoryAt = 4000 - 500;
            (long, long) delay_destoryAt = new(0, destoryAt);
            Vector2 scale = new(32.0f, 70.0f);
            // accessory.FastDraw(DrawTypeEnum.Rect, bossId, scale, delay_destoryAt, false);
            float offsetX = actionId == (uint)DataM7S.AID.LashingLariatWithLeftHand ? -9 : 9;

            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = nameof(P3_LashingLariatDangerousZoneDraw) + Guid.NewGuid().ToString();
            dp.Delay = 0;
            dp.DestoryAt = destoryAt;
            dp.Scale = new Vector2(32.0f, 70.0f);
            dp.Owner = @event.SourceId;
            dp.Offset = new Vector3(offsetX, 0, 0);
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        }


        [ScriptMethod(name: "P3 野蛮怒视 危险区绘制 P3 Glower Power Dangerous Zone Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3])]
        public void P3_GlowerPowerDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 effectPos = @event.EffectPosition;
            if (effectPos.Y > -100) return; // 只在P3绘制
            if (P3_BrutishSwingCastedCount == 1)
            {
                long destoryAt = @event.SourcePosition.Y < -100 ? 5700 : 7600;
                (long, long) delay_destoryAtGlower = new(destoryAt, 4100);
                // (long, long) delay_destoryAtRect = new (0, destoryAt - 1300);
                float radius_AOE = 6.0f;
                accessory.FastDraw(DrawTypeEnum.Rect, @event.SourceId, new Vector2(14.0f, 65.0f), delay_destoryAtGlower, false);
                foreach (uint playerId in accessory.Data.PartyList)
                {
                    ulong _id = (ulong)playerId;
                    // 过滤一下死掉的哥们
                    IGameObject obj = accessory.Data.Objects.SearchById(_id);
                    if (obj is null || obj.IsDead)
                    {
                        continue;
                    }
                    accessory.FastDraw(DrawTypeEnum.Circle, _id, new Vector2(radius_AOE, radius_AOE), delay_destoryAtGlower, false);
                }
            }
        }

        [ScriptMethod(name: "P3 野蛮怒视 指路绘制 P3 Glower Power Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.BrutishSwingActionId_P2P3])]
        public void P3_GlowerPowerGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 effectPos = @event.EffectPosition;
            if (effectPos.Y > -100) return; // 只在P3绘制
            if (P3_BrutishSwingCastedCount == 1)
            {
                // 这意味着是第二次跳跃，有嘴炮的那次
                Vector3 myStartPos = DataM7S.P3_FieldCenter;
                Vector3 myEndPos = DataM7S.P3_FieldCenter;
                EX.PlayerRoleEnum myRole = accessory.GetMyRole();
                bool isInSafe = @event.ActionId == (uint)DataM7S.AID.BrutishSwingMachete_P3;
                Vector3 inLeft = new Vector3(-10, 0, -16) + DataM7S.P3_FieldCenter;
                Vector3 inRight = new Vector3(10, 0, -16) + DataM7S.P3_FieldCenter;
                Vector3 outLeft = new Vector3(-10, 0, -10) + DataM7S.P3_FieldCenter;
                Vector3 outRight = new Vector3(10, 0, -10) + DataM7S.P3_FieldCenter;
                // 假设BOSS跳到了正北方向的墙面
                (myStartPos, myEndPos) = (myRole, WalkthroughType, isInSafe) switch
                {
                    (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, true) => (inLeft, new Vector3(-7.5f, 0, -14) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, false) => (outLeft, new Vector3(-7.5f, 0, -14) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, true) => (inRight, new Vector3(7.5f, 0, -14) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, false) => (outRight, new Vector3(7.5f, 0, -14) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, true) => (inLeft, new Vector3(-7.5f, 0, -5) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, false) => (new Vector3(-7.5f, 0, -5) + DataM7S.P3_FieldCenter, new Vector3(-7.5f, 0, -5) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, true) => (inRight, new Vector3(7.5f, 0, -5) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, false) => (new Vector3(7.5f, 0, -5) + DataM7S.P3_FieldCenter, new Vector3(7.5f, 0, -5) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, true) => (inLeft, new Vector3(-15, 0, -19) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, false) => (outLeft, new Vector3(-15, 0, -19) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, true) => (inRight, new Vector3(15, 0, -19) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, false) => (outRight, new Vector3(15, 0, -19) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, true) => (inLeft, new Vector3(-19, 0, -11) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, false) => (new Vector3(-10, 0, 10) + DataM7S.P3_FieldCenter, new Vector3(-10, 0, 10) + DataM7S.P3_FieldCenter),

                    (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, true) => (inRight, new Vector3(15, 0, -9) + DataM7S.P3_FieldCenter),
                    (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, false) => (new Vector3(10, 0, 10) + DataM7S.P3_FieldCenter, new Vector3(10, 0, 10) + DataM7S.P3_FieldCenter),
                    _ => (myStartPos, myEndPos) // 默认值
                };

                // 根据effectPos 转一下
                float _rot = MathF.Atan2(effectPos.Z - DataM7S.P3_FieldCenter.Z, effectPos.X - DataM7S.P3_FieldCenter.X);
                float modRot = _rot + MathF.PI / 2;
                myStartPos = Util.RotatePointInFFXIV(myStartPos, DataM7S.P3_FieldCenter, modRot);
                myEndPos = Util.RotatePointInFFXIV(myEndPos, DataM7S.P3_FieldCenter, modRot);
                accessory.MultiDisDraw(new List<EX.DisplacementContainer>
                {
                    new(myStartPos, 0, 5000),
                    new(myEndPos, 0, 5000)
                }, MultiDisProp);
            }
        }

        [ScriptMethod(name: "P3 荆棘生死战·墙面 指路绘制 P3 DebrisDeathmatch Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.DebrisDeathmatchActionId])]
        public void P3_DebrisDeathmatchGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 myEndPos = DataM7S.P3_FieldCenter;
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            myEndPos = (myRole, WalkthroughType) switch
            {
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(0, 0, -17),
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(0, 0, 17),
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(-17, 0, 0),
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(17, 0, 0),
                _ => myEndPos // 或抛出异常，也可以返回默认值，比如 DataM7S.P3_FieldCenter
            };
            if (myEndPos == DataM7S.P3_FieldCenter) return;
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> { new(myEndPos, 0, 4000) }, MultiDisProp);
        }
        [ScriptMethod(name: "P3 种弹播撒(孢囊) 指路绘制 P3 Pollen Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.PollenActionId])]
        public void P3_PollenGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.EffectPosition;
            if (pos.Y > -100) return; // 只在P3阶段绘制
            if (Util.DistanceByTwoPoints(pos, DataM7S.P3_FieldCenter) < 22.5f
                || pos.Z < DataM7S.P3_FieldCenter.Z) return; // 只检测左下角或者右下角
            bool isMeGetThornyDeathmatch = accessory.Data.MyObject is not null
                && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV));
            if (!isMeGetThornyDeathmatch) return; // 只在有连线的情况下绘制
            bool isLeftDownSafe = pos.X > DataM7S.P3_FieldCenter.X;
            List<Vector3> safePosList = isLeftDownSafe ?
                new()
                {
                    new Vector3(-19, 0, 19) + DataM7S.P3_FieldCenter,
                    new Vector3(19, 0, -19) + DataM7S.P3_FieldCenter,
                } :
                new()
                {
                    new Vector3(19, 0, 19) + DataM7S.P3_FieldCenter,
                    new Vector3(-19, 0, -19) + DataM7S.P3_FieldCenter,
                };
            // 就近指向点位
            Vector3 _myPos = safePosList.OrderBy(p => Util.DistanceByTwoPoints(p, accessory.Data.MyObject.Position)).FirstOrDefault();
            // 点位垂直或者水平平移一个modLength
            List<Vector3> stackPosList = new()
            {
                DataM7S.P3_FieldCenter + new Vector3(0, 0, -25),
                DataM7S.P3_FieldCenter + new Vector3(0, 0, 25),
                DataM7S.P3_FieldCenter + new Vector3(-25, 0, 0),
                DataM7S.P3_FieldCenter + new Vector3(25, 0, 0)
            };
            Vector3 _myStackPos = stackPosList.OrderBy(p => Util.DistanceByTwoPoints(p, accessory.Data.MyObject.Position)).FirstOrDefault();
            Vector3 myStackPos = (_myStackPos - DataM7S.P3_FieldCenter) * 0.76f + DataM7S.P3_FieldCenter; // 缩小到0.76倍
            Vector3 myPos = (_myPos - myStackPos) * 0.72f + myStackPos; // 缩小到0.72倍
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> {
                new(myPos, 0, 4000),
                new(myStackPos, 0, 4000),
                }, MultiDisProp);

        }
        [ScriptMethod(name: "P3 种弹重击(分摊冰花) 指路绘制 Killer Seeds Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.KillerSeedsActionId],
            suppress: 1000)]
        public void P3_KillerSeedsGuideDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 bossPos = @event.SourcePosition;
            if (bossPos.Y > -100) return; // 只在P3阶段绘制

            bool _isMeGetThornyDeathmatch = accessory.Data.MyObject is not null
                && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV));
            if (_isMeGetThornyDeathmatch) return; // 连线玩家的绘制在前一个

            Vector3 myPos = Vector3.Zero;
            Vector3 groupD3 = new Vector3(0, 0, -19);
            Vector3 groupD4 = new Vector3(0, 0, 19);
            Vector3 groupH1 = new Vector3(-19, 0, 0);
            Vector3 groupH2 = new Vector3(19, 0, 0);
            switch (WalkthroughType)
            {
                case WalkthroughEnum.MMW_SPJP:
                    myPos = accessory.GetMyRole() switch
                    {
                        EX.PlayerRoleEnum.MT => groupD3,
                        EX.PlayerRoleEnum.ST => groupD4,
                        EX.PlayerRoleEnum.H1 => groupH1,
                        EX.PlayerRoleEnum.H2 => groupH2,
                        EX.PlayerRoleEnum.D1 => groupH1,
                        EX.PlayerRoleEnum.D2 => groupH2,
                        EX.PlayerRoleEnum.D3 => groupD3,
                        EX.PlayerRoleEnum.D4 => groupD4,
                        _ => Vector3.Zero
                    };
                    myPos += DataM7S.P3_FieldCenter;
                    bool isMeGetThornyDeathmatch = accessory.Data.MyObject is not null
                        && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                        || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                        || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                        || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV));
                    if (isMeGetThornyDeathmatch)
                    {
                        // 我有连线, 去最近的分摊点位
                        List<Vector3> posList = new()
                        {
                            new Vector3(0, 0, -25) + DataM7S.P3_FieldCenter,
                            new Vector3(0, 0, 25) + DataM7S.P3_FieldCenter,
                            new Vector3(-25, 0, 0) + DataM7S.P3_FieldCenter,
                            new Vector3(25, 0, 0) + DataM7S.P3_FieldCenter
                        };
                        posList = posList.OrderBy(p => Util.DistanceByTwoPoints(p, accessory.Data.MyObject.Position)).ToList();
                        Vector3 _modPos = posList[0] - DataM7S.P3_FieldCenter;
                        myPos = _modPos * 0.76f + DataM7S.P3_FieldCenter; // 缩小到0.76倍
                    }
                    // 绘制指路
                    EX.DisplacementContainer myEndPos = new(myPos, 0, 4700);
                    accessory.MultiDisDraw(new List<EX.DisplacementContainer> { myEndPos }, MultiDisProp);
                    break;
            }

        }

        [ScriptMethod(name: "P3 种弹播撒(预指路) 指路绘制 Sinister Seeds Blossom Pre Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsVisualActionId])]
        public void P3_SinisterSeedsBlossomPreGuideDraw(Event @event, ScriptAccessory accessory)
        {
            SinisterSeedTargets.Clear(); // 清空冰花目标
            if (@event.SourcePosition.Y > -100) return; // 只在P3阶段绘制
            bool isMeGetThornyDeathmatch = accessory.Data.MyObject is not null
                && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV));
            if (!isMeGetThornyDeathmatch)
            {
                switch (WalkthroughType)
                {
                    case WalkthroughEnum.MMW_SPJP:
                        accessory.MultiDisDraw(new List<EX.DisplacementContainer> {
                            new(new Vector3(10, 0, -4) + DataM7S.P3_FieldCenter, 0, 4900),
                            }, MultiDisProp);
                        break;
                }
            }
        }

        [ScriptMethod(name: "P3 种弹播撒(紫圈冰花) 指路绘制 Sinister Seeds Blossom Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsBlossomActionId42350])]
        public void P3_SinisterSeedsBlossomGuideDraw(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y > -100) return; // 只在P3阶段绘制
            ulong tarId = @event.TargetId;
            int count = -1;
            lock (_lock)
            {
                if (!SinisterSeedTargets.Contains(tarId))
                {
                    accessory.Log.Debug($"P3 Sinister Seeds Blossom Guide Draw : add targetId => {tarId}");
                    SinisterSeedTargets.Add(tarId); // 记录冰花目标
                    // SinisterSeedTargets.Add(tarId); // 记录冰花目标
                    count = SinisterSeedTargets.Count;
                }
                else
                {
                    accessory.Log.Debug($"P3 Sinister Seeds Blossom Guide Draw : targetId => {tarId} already exists");
                    accessory.Log.Debug($"P3 Sinister Seeds Blossom Guide Draw : SinisterSeedTargets count => {SinisterSeedTargets.Count}, targetId => {tarId}");
                    // count = SinisterSeedTargets.Count; // 记录冰花目标
                }
            }
            accessory.Log.Debug($"P3 Sinister Seeds Blossom Guide Draw : count => {count}, targetId => {tarId}");
            if (count != 4) return;
            bool isMeGetThornyDeathmatch = accessory.Data.MyObject is not null
                && (accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchI)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIII)
                || accessory.Data.MyObject.HasStatus((uint)DataM7S.SID.ThornyDeathmatchIV));
            bool isMeGetSinisterSeed = SinisterSeedTargets.Contains((ulong)accessory.Data.Me);
            Vector3 endPos = Vector3.Zero;
            if (isMeGetSinisterSeed)
            {
                // 被点了紫圈 
                if (isMeGetThornyDeathmatch)
                {
                    // 我有连线, 去最近的分摊点位
                    List<Vector3> posList = new()
                    {
                        new Vector3(0, 0, -25) + DataM7S.P3_FieldCenter,
                        new Vector3(0, 0, 25) + DataM7S.P3_FieldCenter,
                        new Vector3(-25, 0, 0) + DataM7S.P3_FieldCenter,
                        new Vector3(25, 0, 0) + DataM7S.P3_FieldCenter
                    };
                    posList = posList.OrderBy(pos => Util.DistanceByTwoPoints(pos, accessory.Data.MyObject.Position)).ToList();
                    Vector3 _modPos = posList[0] - DataM7S.P3_FieldCenter;
                    endPos = _modPos * 0.76f + DataM7S.P3_FieldCenter;
                }
                else
                {
                    endPos = (accessory.GetMyRole(), WalkthroughType) switch
                    {
                        (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(-10, 0, -10),
                        (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(10, 0, -10),
                        (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(-10, 0, 10),
                        (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP) => DataM7S.P3_FieldCenter + new Vector3(10, 0, 10),
                        _ => Vector3.Zero
                    };
                }
                accessory.MultiDisDraw(new List<EX.DisplacementContainer> { new(endPos, 0, 6900) }, MultiDisProp);
            }
            else
            {
                if (isMeGetThornyDeathmatch) return;
                // 没有被点名, 且我没连线
                switch (WalkthroughType)
                {
                    case WalkthroughEnum.MMW_SPJP:
                        accessory.MultiDisDraw(new List<EX.DisplacementContainer> {
                            // new(new Vector3(10, 0, -4) + DataM7S.P3_FieldCenter, 0, 2000),
                            new(new Vector3(16, 0, -10) + DataM7S.P3_FieldCenter, 0, 2000),
                            new(new Vector3(10, 0, -16) + DataM7S.P3_FieldCenter, 0, 2000),
                            new(new Vector3(4, 0, -10) + DataM7S.P3_FieldCenter, 0, 2000)
                            }, MultiDisProp);
                        break;
                }

            }
        }

        [ScriptMethod(name: "P3 种弹炸裂(黄圈冰花/两轮冰花) 预站位 指路绘制 Sinister Seeds Blossom (Yellow) Pre Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.StrangeSeedsVisualActionId_P3])]
        public void P3_SinisterSeedsBlossomGuideDraw2_Pre(Event @event, ScriptAccessory accessory)
        {
            // 在boss读条种弹播撒的时候，绘制预站位, 透明度调整为50%
            if (@event.SourcePosition.Y > -100) return; // 只在P3阶段绘制


            Vector3 endPos = DataM7S.P3_FieldCenter;
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            bool isFieldBasis = true;

            endPos = (myRole, WalkthroughType, isFieldBasis) switch
            {
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, -10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, -10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, 10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, 10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, 10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, 10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, -10) * 0.7f + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, -10) * 0.7f + DataM7S.P3_FieldCenter,
                _ => endPos
            };
            EX.MultiDisDrawProp tempProp = new()
            {
                BaseDelay = MultiDisProp.BaseDelay,
                Width = MultiDisProp.Width,
                EndCircleRadius = MultiDisProp.EndCircleRadius,
                Color_GoNow = MultiDisProp.Color_GoNow.WithW(0.5f), // 透明度调整为50%
                Color_GoLater = MultiDisProp.Color_GoLater.WithW(0.5f), // 透明度调整为50%
                DrawMode = MultiDisProp.DrawMode,
            };
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> { new(endPos, 0, 3500) }, tempProp);
        }

        [ScriptMethod(name: "P3 种弹炸裂(黄圈冰花/两轮冰花) 指路绘制 Sinister Seeds Blossom (Yellow) Guide Draw",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: [DataM7S.SinisterSeedsBlossomActionId42392])]
        public void P3_SinisterSeedsBlossomGuideDraw2(Event @event, ScriptAccessory accessory)
        {
            if (@event.SourcePosition.Y > -100)
            {
                if (P3MMWZhuiCheDebug) accessory.Method.SendChat("/e 检测到黄圈冰花，但不是P3阶段");
                return; // 只在P3阶段绘制
            }
            if (@event.TargetId != (ulong)accessory.Data.Me)
            {
                // 不是自己的冰花, 不进行额外绘制
                if (P3MMWZhuiCheDebug) accessory.Method.SendChat("/e 检测到黄圈冰花，但这个黄圈的目标不是你");
                return;
            }
            EX.PlayerRoleEnum myRole = accessory.GetMyRole();
            if (P3MMWZhuiCheDebug) accessory.Method.SendChat($"/e 你的职能是 {myRole}");
            if (P3MMWZhuiCheDebug) accessory.Method.SendChat($"/e 采用攻略是 {WalkthroughType}");
            Vector3 endPos = Vector3.Zero;
            Vector3 bossPos = @event.SourcePosition;
            // Vector3 bossToC = DataM7S.P3_FieldCenter - bossPos;
            // 但是这个是冲之前的位置，给BOSS冲一下
            Vector3 P3MMWZhuiChe_MTD1 = new Vector3(10, 0, -10);
            Vector3 P3MMWZhuiChe_STD2 = new Vector3(10, 0, 10);
            Vector3 P3MMWZhuiChe_H1D3 = new Vector3(-10, 0, -10);
            Vector3 P3MMWZhuiChe_H2D4 = new Vector3(-10, 0, 10);

            Vector3 bossPosToC = DataM7S.P3_FieldCenter - bossPos;
            float bossPosRot = MathF.Atan2(bossPosToC.Z, bossPosToC.X);

            bool isSecondRound = false;
            isSecondRound = Util.DistanceByTwoPoints(bossPos, DataM7S.P3_FieldCenter) > 10; //accessory.Data.Objects.GetByDataId((uint)DataM7S.OID.BruteAbombinator).Any(obj => obj is IBattleChara bc && bc.IsCasting);

            bool isFieldBasis = !isSecondRound || (isSecondRound && !P3MMWZhuiChe);
            if (P3MMWZhuiCheDebug) accessory.Method.SendChat($"/e 是第{(isSecondRound ? 2 : 1)}轮冰花，追车吗?({(isFieldBasis ? "否" : "是")})");

            endPos = (myRole, WalkthroughType, isFieldBasis) switch
            {
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, -10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.MT, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_MTD1, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, -10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.ST, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_STD2, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, 10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H1, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_H1D3, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, 10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.H2, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_H2D4, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, 10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D1, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_MTD1, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, 10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D2, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_STD2, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, true) => new Vector3(-10, 0, -10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D3, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_H1D3, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, true) => new Vector3(10, 0, -10) + DataM7S.P3_FieldCenter,
                (EX.PlayerRoleEnum.D4, WalkthroughEnum.MMW_SPJP, false) => Util.RotatePointInFFXIV(P3MMWZhuiChe_H2D4, Vector3.Zero, bossPosRot) + DataM7S.P3_FieldCenter,
                _ => endPos + DataM7S.P3_FieldCenter
            };
            if (P3MMWZhuiCheDebug) accessory.Method.SendChat($"/e 你的就位点是 {endPos}");
            accessory.MultiDisDraw(new List<EX.DisplacementContainer> { new(endPos, 0, 5200) }, MultiDisProp);
        }


        /*
                P2P3, 为被线连着的玩家画一条分界线, 1层2层
            */



        /*
            P3孢子爆炸绘制危险区
            ↑同P1, 不进行额外绘制
        */

        /*
            P3分摊冰花绘制危险区
            P3分摊冰花落地后绘制危险区
            ↑同P2,不进行额外绘制
        */
        /*
            石化绘制安全区
            ↑同P1, 不进行额外绘制
        */
        /*
            潜地炮+冰花绘制危险区
            ↑同P1, 不进行额外绘制
        */

        // 剩余工作 
        // P3的嘴炮指路
        // P3碎颈臂冲是否提前
        // 记录双武器类型，记录碎颈冲前的武器跳跃类型
        // P3黄圈冰花第二轮提前指路


    }


    #region 拓展方法
    public static class ScriptExtensions_Tsing
    {

        // 快速绘图
        public static void FastDraw(this ScriptAccessory accessory, DrawTypeEnum drawType, Vector3 position, Vector2 scale, (long Delay, long DestoryAt) delay_destoryAt, Vector4 color, DrawModeEnum drawMode = DrawModeEnum.Default)
        {
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = Guid.NewGuid().ToString();
            dp.Delay = delay_destoryAt.Delay;
            dp.DestoryAt = delay_destoryAt.DestoryAt;
            dp.Color = color;
            dp.Scale = scale;
            switch (drawType)
            {
                case DrawTypeEnum.Donut:
                    dp.Scale = new(scale.X);
                    dp.InnerScale = new(scale.Y);
                    dp.Radian = 2 * MathF.PI;
                    break;
            }

            dp.Position = position;
            accessory.Method.SendDraw(drawMode, drawType, dp);
        }
        public static void FastDraw(this ScriptAccessory accessory, DrawTypeEnum drawType, ulong ownerId, Vector2 scale, (long Delay, long DestoryAt) delay_destoryAt, Vector4 color, DrawModeEnum drawMode = DrawModeEnum.Default)
        {
            DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = Guid.NewGuid().ToString();
            dp.Delay = delay_destoryAt.Delay;
            dp.DestoryAt = delay_destoryAt.DestoryAt;
            dp.Color = color;
            dp.Scale = scale;
            switch (drawType)
            {
                case DrawTypeEnum.Donut:
                    dp.Scale = new(scale.X);
                    dp.InnerScale = new(scale.Y);
                    dp.Radian = 2 * MathF.PI;
                    break;
            }

            dp.Owner = ownerId;
            accessory.Method.SendDraw(drawMode, drawType, dp);
        }
        public static void FastDraw(this ScriptAccessory accessory, DrawTypeEnum drawType, Vector3 position, Vector2 scale, (long Delay, long DestoryAt) delay_destoryAt, bool isSafe, DrawModeEnum drawMode = DrawModeEnum.Default)
        {
            Vector4 color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            accessory.FastDraw(drawType, position, scale, delay_destoryAt, color, drawMode);
        }
        public static void FastDraw(this ScriptAccessory accessory, DrawTypeEnum drawType, ulong ownerId, Vector2 scale, (long Delay, long DestoryAt) delay_destoryAt, bool isSafe, DrawModeEnum drawMode = DrawModeEnum.Default)
        {
            Vector4 color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            accessory.FastDraw(drawType, ownerId, scale, delay_destoryAt, color, drawMode);
        }


        public class DisplacementContainer
        {
            public Vector3 Pos;
            public long Delay;
            public long DestoryAt;

            private DisplacementContainer() { }
            public DisplacementContainer(Vector3 pos, long delay, long destoryAt)
            {
                this.Pos = pos;
                this.Delay = delay;
                this.DestoryAt = destoryAt;
            }
        }
        public class MultiDisDrawProp
        {
            public Vector4 Color_GoNow;
            public Vector4 Color_GoLater;
            public long BaseDelay;
            public float Width;
            public float EndCircleRadius;
            public DrawModeEnum DrawMode;

            public MultiDisDrawProp()
            {
                this.Color_GoNow = new(1, 1, 1, 1);
                this.Color_GoLater = new(0, 1, 1, 1);
                this.BaseDelay = 0;
                this.Width = 1.2f;
                this.EndCircleRadius = 0.65f;
                this.DrawMode = DrawModeEnum.Default;
            }
        }
        internal static void MultiDisDraw(this ScriptAccessory accessory, List<DisplacementContainer> list, MultiDisDrawProp prop)
        {
            // accessory.Log.Debug("RawMultiDisDraw");
            long startTimeMillis = prop.BaseDelay;
            const long preMs = 270;
            string guid = Guid.NewGuid().ToString();
            for (int i = 0; i < list.Count; i++)
            {
                int count = 0;
                DisplacementContainer dis = list[i];
                string name = $"_MultiDisDraw Part {i} : {guid} / ";

                // go now 直线引导部分
                DrawPropertiesEdit dp_goNowLine = accessory.Data.GetDefaultDrawProperties();
                dp_goNowLine.Name = name + count++;
                dp_goNowLine.Owner = (ulong)accessory.Data.Me;
                dp_goNowLine.Scale = new(prop.Width);
                dp_goNowLine.Delay = startTimeMillis + dis.Delay - Math.Sign(i) * preMs;
                dp_goNowLine.DestoryAt = dis.DestoryAt - preMs / 3 - (prop.DrawMode == DrawModeEnum.Imgui ? 200 : 0);
                dp_goNowLine.ScaleMode |= ScaleMode.YByDistance;
                dp_goNowLine.TargetPosition = dis.Pos;
                dp_goNowLine.Color = prop.Color_GoNow;
                accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Displacement, dp_goNowLine);
                // accessory.Log.Debug($"dp_goNowLine.Delay = {dp_goNowLine.Delay}");
                if (prop.EndCircleRadius > 0)
                {
                    DrawPropertiesEdit dp_goNowCircle = accessory.Data.GetDefaultDrawProperties();
                    dp_goNowCircle.Name = name + count++;
                    // dp_goNowCircle.Owner = (ulong)accessory.Data.Me;
                    dp_goNowCircle.Position = dis.Pos;
                    dp_goNowCircle.Scale = new(prop.EndCircleRadius);
                    dp_goNowCircle.Delay = dp_goNowLine.Delay;
                    dp_goNowCircle.DestoryAt = dp_goNowLine.DestoryAt;
                    dp_goNowCircle.Color = prop.Color_GoNow;
                    accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Circle, dp_goNowCircle);
                }

                //如果当前点位不是第一个点位，则进行go later部分
                if (i >= 1)
                {
                    // DisplacementContainer disBefore = _list[i - 1];
                    DrawPropertiesEdit dp_goLaterLine = accessory.Data.GetDefaultDrawProperties();
                    dp_goLaterLine.Name = name + count++;
                    dp_goLaterLine.Position = list[i - 1].Pos;
                    dp_goLaterLine.TargetPosition = dis.Pos;
                    dp_goLaterLine.Scale = new(prop.Width);
                    dp_goLaterLine.ScaleMode |= ScaleMode.YByDistance;
                    dp_goLaterLine.Delay = prop.BaseDelay + list[0].Delay;
                    dp_goLaterLine.DestoryAt = startTimeMillis - (prop.BaseDelay + list[0].Delay) - 100 - (prop.DrawMode == DrawModeEnum.Imgui ? 200 : 0);
                    dp_goLaterLine.Color = prop.Color_GoLater;
                    accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Displacement, dp_goLaterLine);

                    if (prop.EndCircleRadius > 0)
                    {

                        DrawPropertiesEdit dp_goLaterCircle = accessory.Data.GetDefaultDrawProperties();
                        dp_goLaterCircle.Name = name + count++;
                        dp_goLaterCircle.Position = dis.Pos;
                        dp_goLaterCircle.Scale = new(prop.EndCircleRadius);
                        dp_goLaterCircle.Delay = dp_goLaterLine.Delay;
                        dp_goLaterCircle.DestoryAt = dp_goLaterLine.DestoryAt - 100;
                        dp_goLaterCircle.Color = prop.Color_GoLater;
                        accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Circle, dp_goLaterCircle);
                    }
                }
                startTimeMillis = startTimeMillis + dis.Delay + dis.DestoryAt;

            }

        }

        public enum PlayerRoleEnum
        {
            MT = 0, ST = 1,
            H1 = 2, H2 = 3,
            D1 = 4, D2 = 5, D3 = 6, D4 = 7,
            Unknown = -1
        }

        public static PlayerRoleEnum GetMyRole(this ScriptAccessory accessory)
        {
            uint myId = accessory.Data.Me;
            if (accessory.Data.PartyList is null) return PlayerRoleEnum.MT;
            List<uint> partyList = new List<uint>(accessory.Data.PartyList);
            int myIndex = partyList.IndexOf(myId);
            accessory.Log.Debug($"GetMyRole: myId = {myId}, myIndex = {myIndex}, partyList.Count = {partyList.Count}");
            if (myIndex < 0 || myIndex >= partyList.Count)
            {
                accessory.Method.SendChat($"/e 获取职能异常，你的序号值为 {myIndex} (该值的范围应当是 0 - 7)");
            }
            if (Enum.IsDefined(typeof(PlayerRoleEnum), myIndex))
            {
                return (PlayerRoleEnum)myIndex;
            }
            else
            {
                return PlayerRoleEnum.Unknown;
            }


        }
        public static Job MyJob(this ScriptAccessory accessory)
        {
            try
            {
                IPlayerCharacter? myChara = accessory.Data.MyObject;
                if (myChara is not null && Enum.IsDefined(typeof(Job), (byte)myChara.ClassJob.RowId))
                {
                    return (Job)myChara.ClassJob.RowId;
                }
                else
                {
                    return Job.ADV;
                }
            }
            catch (Exception ex)
            {
                accessory.Log.Error($"获取职业失败: {ex.Message}");
                return Job.ADV;
            }

        }
        
        public enum Job : byte
        {
            ADV = 0, GLA = 1, PGL = 2, MRD = 3, LNC = 4, ARC = 5, CNJ = 6, THM = 7, CRP = 8, BSM = 9,
            ARM = 10, GSM = 11, LTW = 12, WVR = 13, ALC = 14, CUL = 15, MIN = 16, BTN = 17, FSH = 18,
            PLD = 19, MNK = 20, WAR = 21, DRG = 22, BRD = 23, WHM = 24, BLM = 25,
            ACN = 26,
            SMN = 27, SCH = 28,
            ROG = 29,
            NIN = 30, MCH = 31, DRK = 32, AST = 33, SAM = 34, RDM = 35, BLU = 36, GNB = 37, DNC = 38,
            RPR = 39, SGE = 40, VPR = 41, PCT = 42,
        }
    
    }
    public static class Utilities_Tsing
    {
        public static float DistanceByTwoPoints(Vector3 point1, Vector3 point2)
        {
            float x = point1.X - point2.X;
            float y = point1.Y - point2.Y;
            float z = point1.Z - point2.Z;
            return MathF.Sqrt(MathF.Pow(x, 2) + MathF.Pow(y, 2) + MathF.Pow(z, 2));
        }
        public static Vector3 RotatePointInFFXIV(Vector3 point, Vector3 centre, float radian)
        {
            Vector3 cToP = point - centre;
            Vector2 cToP_v2 = new(cToP.X, cToP.Z);
            float rot = (MathF.Atan2(cToP_v2.Y, cToP_v2.X) + radian);
            float length = cToP_v2.Length();
            return new(centre.X + MathF.Cos(rot) * length, centre.Y, centre.Z + MathF.Sin(rot) * length);
        }
    }
    #endregion

    #region 数据存放
    public static class DataM7S
    {
        public const string BossDataId = $"DataId:regex:^(18307)$";
        public const string BrutalImpactActionId = $"ActionId:regex:^(42331)$";
        public const string SmashHereThereActionId = $"ActionId:regex:^(42335|42336)$";
        public const string BrutishSwingActionId_P1 = $"ActionId:regex:^(42337|42338)$";
        public const string P1_StoneringerActionId = $"ActionId:regex:^(42333|42334)$";
        public const string P3_StoneringerActionId = $"ActionId:regex:^(42401|42400)$";

        public const string PollenActionId = $"ActionId:regex:^(42347)$";
        public const string RootsOfEvilActionId = $"ActionId:regex:^(42354)$";
        public const string SinisterSeedsVisualActionId = $"ActionId:regex:^(42349)$";
        public const string SinisterSeedsBlossomActionId = $"ActionId:regex:^(42350|42392|42395)$";
        public const string SinisterSeedsChaseActionId = $"ActionId:regex:^(42353)$";
        public const string SinisterSeedsBlossomActionId42350 = $"ActionId:regex:^(42350)$";
        public const string SinisterSeedsBlossomActionId42392 = $"ActionId:regex:^(42392)$";
        public const string KillerSeedsActionId = $"ActionId:regex:^(42395)$";
        public const string TendrilsOfTerrorActionId = $"ActionId:regex:^(42351|42393|42396)$";
        public const string QuarrySwampActionId = $"ActionId:regex:^(42357)$";
        public const string BloomingAbominationDataId = $"DataId:regex:^(18308)$";
        
        public const string MobsWindsActionId = $"ActionId:regex:^(43277|43278)$";
        public const string ExplosionActionId = $"ActionId:regex:^(42358)$";
        public const string PulpSmashActionId = $"ActionId:regex:^(42359)$";
        public const string ItCameFromTheDirtActionId = $"ActionId:regex:^(42362)$";
        public const string NeoBombarianSpecialActionId = $"ActionId:regex:^(42364)$";
        public const string BrutishSwingActionId_P2P3 = $"ActionId:regex:^(42386|42387|42403|42405)$";
        public const string GlowerPowerActionId = $"ActionId:regex:^(43340)$";
        public const string DemolitionDeathmatchActionId = $"ActionId:regex:^(42390)$";
        public const string DebrisDeathmatchActionId = $"ActionId:regex:^(42416)$";
        public const string AbominableBlinkActionId = $"ActionId:regex:^(42377)$";
        public const string AbominableBlinkIconId = "Id:regex:^(0147)$";
        public const string StrangeSeedsActionId = $"ActionId:regex:^(42392)$";
        public const string LashingLariatActionId = $"ActionId:regex:^(42408|42410)$";
        public const string StrangeSeedsVisualActionId_P3 = $"ActionId:regex:^(43274)$";
        public const string StrangeSeedsVisualActionId_P2 = $"ActionId:regex:^(42391)$";
        // public const string BrutishSwingActionId_P3 = $"ActionId:regex:^(42403|42405)$";


        // P1 场地相关数据
        public static readonly Vector3 P1_FieldCenter = new Vector3(100, 0, 100);
        public static readonly Vector3 P1_NailOffset_In = new Vector3(9, 0, 9); // P1 钉子的偏移
        public static readonly Vector3 P1_NailOffset_Out = new Vector3(18, 0, 18); // P1 钉子的偏移
        public static readonly Vector2 P1_FieldSideLength = new Vector2(40, 40); // P1 场地边长

        // P2 场地相关数据
        public static readonly Vector3 P2_FieldCenter = new Vector3(100, 0, 5);
        public static readonly Vector3 P2_NailOffset_In = new Vector3(9, 0, 14); // P2 钉子的偏移
        public static readonly Vector3 P2_NailOffset_Out = new Vector3(11, 0, 23.5f); // P2 钉子的偏移
        public static readonly Vector2 P2_FieldSideLength = new Vector2(25, 50); // P2 场地边长

        // P3 场地相关数据
        public static readonly Vector3 P3_FieldCenter = new Vector3(100, -200, 5);
        public static readonly Vector2 P3_SquareSideLength = new Vector2(5, 5); // P3 场地中小方块的边长
        public static readonly Vector2 P3_FieldSideLength = new Vector2(40, 40); // P3 场地边长

        public enum AID : uint
        {
            BrutalImpact = 42331, // Boss->self, 5.0s cast, single-target 全屏AOE
            StoneringerStick = 42333, // 掏出大棒
            StoneringerMachete = 42334, // 掏出大刀
            SmashHere = 42335, // 打近死刑, cast 2.7s circle 7y
            SmashThere = 42336, // 打远死刑, cast 2.7s circle 7y
            BrutishSwingStick_P1 = 42337, // P1大棒的钢铁AOE
            BrutishSwingMachete_P1 = 42338, // P2大刀的月环AOE
            Pollen = 42347, // P1中BOSS在地上放置孢子,cast 3.7s, circle 8y
            RootsOfEvil = 42354, // P1中潜地炮变大后的AOE, cast 2.7s , circle 12y
            SinisterSeedsBlossom = 42350, // 冰花点名, cast 6.7s, 4条直线 ,4y
            SinisterSeedsChase = 42353, // 潜地炮点名, cast 2.7s, 8y
            TendrilsOfTerror_P1 = 42351, // 冰花落地后蔓延开的AOE, 4条直线 ,4y

            WindingWildwinds = 43277, // 小怪读条,月环 cast 6.7s
            CrossingCrosswinds = 43278, // 小怪读条,十字 cast 6.7s
            QuarrySwamp = 42357, // BOSS 石化读条, cast 3.7s
            Explosion = 42358, // 三连衰减AOE, cast 8.7s
            PulpSmash = 42359, // 跳跃分摊, cast 2.7s
            ItCameFromTheDirt = 42362, // 跳跃分摊后的脚下钢铁+八方扇形 cast 1.7s
            NeoBombarianSpecial = 42364, // P1末尾转场AOE
            BrutishSwingJump_P2 = 42381, // P2跳跃到别的墙面 cast 3.7s
            BrutishSwingStick_P2 = 42386, //P2 大棒钢铁AOE
            BrutishSwingMachete_P2 = 42387, // P2 月环AOE
            GlowerPower_Straight_P2 = 42373, // BOSS本体嘴炮, cast 2.4s

            AbominableBlink = 42377, // GA-100 ,cast 5.0s
            DemolitionDeathmatch = 42390, // Boss->self, 3.0s cast, single-target 连三个人的那个
            GlowerPower_Electrogenetic_P2 = 43340, // 分身读条的分散AOE cast 3.7s
            StrangeSeeds = 42392, // P2的点名冰花, cast 4.7s
            TendrilsOfTerror_P2_StrangeSeeds = 42393, // P2 点名冰花落地后的主体读条,搭配两个42394形成米字型 cast 2.7s
            KillerSeeds = 42395, // P2P3的分摊冰花, cast 4.7s,
            TendrilsOfTerror_P2_KillerSeeds = 42396, // 搭配两个42397形成米字型 cast 2.7s
            SinisterSeedsVisual = 42349, // Boss->self, 4.0+1.0s cast, single-target

            BrutishSwingJump_P3 = 42402, // P3跳跃到别的墙面cast 2.7s
            DebrisDeathmatch = 42416, // Boss->self, 3.0s cast, single-target 连四个人的那个

            /* 
                很有可能先大棒和后大棒, 用的是两个不同的ID, P3的嘴炮+分散读条较快
                ↑非也，就是同一个
            */
            Stoneringer2Stoneringers_LStick = 42401, // Boss->self, 2.0+3.5s cast, single-target
            Stoneringer2Stoneringers_RStick = 42400, // Boss->self, 2.0+3.5s cast, single-target
            BrutishSwingStick_P3 = 42403, //P3 大棒钢铁AOE cast 6.4s 后置的大棒
            BrutishSwingMachete_P3 = 42405, // P3 大刀月环AOE cast 6.4s 前置的大刀
            LashingLariat_Unknown = 42407, // 冲锋时BOSS的读条 cast 3.2s
            LashingLariatWithRightHand = 42408, //冲锋，使用右手, 面向BOSS去右侧, cast 3.7s
            LashingLariatWithLeftHand = 42410, //冲锋，使用左手, 面向BOSS去左侧, cast 3.7s
            GlowerPower_Straight_P3 = 43338, // BOSS本体嘴炮, cast 0.4s
            GlowerPower_Electrogenetic_P3 = 43358, // 分身读条的分散AOE cast 1.7s
            P2StrangeSeedsVisual = 42391, // Boss->self, 4.0s cast, single-target 黄圈冰花点名启动读条 P2
            P3StrangeSeedsVisual = 43274, // Boss->self, 4.0s cast, single-target 黄圈冰花点名启动读条 P3
        }
        public enum OID : uint
        {
            BruteAbombinator = 18307, // BOSS
            BloomingAbomination = 18308, // 小怪
        }
        public enum SID : uint
        {
            ThornyDeathmatchI = 4466, // 荆棘生死战I, 不可以转移的那个类型
            ThornyDeathmatchII = 4467, // 荆棘生死战II
            ThornyDeathmatchIII = 4468, // 荆棘生死战III
            ThornyDeathmatchIV = 4469, // 荆棘生死战IV

            ThornsOfDeathI = 4499, // none->player, extra=0x0 坦克身上可以转移的类型
            ThornsOfDeathII = 4500, // none->player, extra=0x0
            ThornsOfDeathIII = 4501, // none->player, extra=0x0
            ThornsOfDeathIV = 4502, // none->player, extra=0x0
        }
    }
    #endregion
}
