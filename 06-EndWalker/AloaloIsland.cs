using System;
using Newtonsoft.Json;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using Dalamud.Utility.Numerics;
using ECommons;
//using ECommons.GameFunctions;
//using ECommons.DalamudServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
//using System.Reflection;


using Util = TsingNamespace.AloaloIsland.TsingUtilities;
using static TsingNamespace.AloaloIsland.ScriptExtensions_Tsing;



namespace TsingNamespace.AloaloIsland
{

    [ScriptType(name: "阿罗阿罗岛绘图+指路", territorys: [1179, 1180], guid: "e3cfc380-edc2-f441-bebe-e9e294f2632e", version: "0.0.0.1", author: "Mao")]
    public class AloaloIslandScript
    {   
        [UserSetting("指路时使用的颜色 => 类型: 立即前往")]
        public ScriptColor GuideColor_GoNow { get; set; } = new() { V4 = new(0, 1, 1, 2) };
        [UserSetting("指路时使用的颜色 => 类型: 稍后前往")]
        public ScriptColor GuideColor_GoLater { get; set; } = new() { V4 = new(1, 1, 0, 2) };
        
        [UserSetting("指路时使用的颜色深度")]
        public float GuideColorDensity { get; set; } = 2;



        // Kod内部小队成员序号(与游戏内小队成员列表顺序无关，与指令/KTeam打开的窗口有关)与职能的对应关系。
        // 0-MT,1-H1,2-D1,3-D2
        // 小队成员序号序号从0开始
        [UserSetting("默认职能顺序")]
        public RoleMarksListEnum RoleMarks4 { get; set; }

        // 用于存放触发器触发的时间戳
        private ConcurrentDictionary<string, long> invokeTimestamp = new ConcurrentDictionary<string, long>();
        // 一个CancellationTokenSource 用于取消延时任务
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly object _lock = new object();

        // Boss1水晶的计数,避免重复指路
        //private int springCrystalsCount = 0;

        // Boss1水晶安全区存储
        private List<Vector2> boss1_springCrystalsSafePoints = new List<Vector2>();

        // Boss1 鲸尾突风读条计数
        // private int boss1_flukeGaleCastingCount = 0;

        // Boss1 水化爆弹读条计数
        //private int boss1_hydrobombCastingCount = 0;

        // Boss1 标记小泡泡机制走位顺逆时针
        private bool boss1_bubbleIsClockwise = false;

        // Boos1 标记钢铁月环+大泡泡(标记的场地北侧靠近中间的两个泡泡的其中一个)
        private Vector3 boss1_twintidesBubbleType = new(0,0,0);

        // Boss1 标记是否已经到了小怪引导后的阶段
        private bool boss1_phaseAfterMob = false;


        // Boss2 boss2的ID
        private uint boss2_bossId = 0;

        // Boss2 散火法读条
        private uint boss2_InfernoTheoremCastingCount = 0;
        private bool boss2_InfernoTheoremCasted = false;

        // Boss2 扫雷机制的地雷位置
        private List<Vector3> boss2_ArcaneMinesList = new List<Vector3>();
        private Vector3 boss2_myTowardsPoint = new (200,-300,0);


        //Boss3 花式装填信息
        private List<uint> boss3_trickReloads = new List<uint>();
        private uint boss3_trickReloadsCount = 0 ;	

        //Boss3 炸弹
        private uint boss3_bombsRound = 0;

        private List<float[]> boss3_rad_distance = new List<float[]>
        {
            new float[]{-0.25f * MathF.PI,100},
            new float[]{0.25f * MathF.PI,100},
            new float[]{0.75f * MathF.PI,100},
            new float[]{-0.75f * MathF.PI,100}
        };
        private List<float> boss3_fireSpreadRotation = new List<float>();
        private List<uint> boss3_burningChainsPlayers = new List<uint>();
        private bool boss3_isFireBallClockwise = false;




        // 存放小队成员的Buff和Debuff类型(StatusID), 气泡网回声3743(泡泡), 气泡凝聚3788(止步)
        // private Dictionary<int, string[]> partyMembersBuffsAndDebufs = new Dictionary<int, string[]>();



        public enum RoleMarksListEnum { MT_H1_D1_D2 }
        public enum RoleMarkEnum { MT, H1, D1, D2 }


        


        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug($"Initialize! => AloaloIslandScript");
            accessory.Method.RemoveDraw(".*");
            //springCrystalsCount = 0;
            boss1_springCrystalsSafePoints.Clear();
            // boss1_flukeGaleCastingCount = 0;
            //boss1_hydrobombCastingCount = 0;
            boss1_bubbleIsClockwise = false;
            boss1_twintidesBubbleType = new (0,0,0);
            boss1_phaseAfterMob = false;
            boss2_InfernoTheoremCastingCount = 0;
            boss2_InfernoTheoremCasted = false;
            boss2_ArcaneMinesList.Clear();
            boss2_myTowardsPoint = new (200,-300,0);
            boss3_trickReloads.Clear();
            boss3_trickReloadsCount = 0;
            //boss3_bombs.Clear();
            boss3_bombsRound = 0;
            boss3_rad_distance = new List<float[]>{new float[]{-0.25f * MathF.PI,100},new float[]{0.25f * MathF.PI,100},new float[]{0.75f * MathF.PI,100},new float[]{-0.75f * MathF.PI,100}};
            boss3_fireSpreadRotation.Clear();
            boss3_burningChainsPlayers.Clear();
            boss3_isFireBallClockwise = false;


            //清除触发时间戳记录+延时任务
            invokeTimestamp.Clear();
            cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();


        }

        #region Mob1
        //给第一组小怪场地上的风圈，绘制危险区与危险区预提醒
        //龙卷技能ID为35776，龙卷模型ID16590
        [ScriptMethod(name: "小怪 1 风圈 Mob 1 Tornado Dangerous Zone Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35776|35791)$"])]
        public void Mob1_TornadoDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            DrawPropertiesEdit propFan = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(5.5f),1000,false);
            propFan.Offset = new(0, 0, -2.7f);
            propFan.Radian = (float)(Math.PI);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, propFan);

            DrawPropertiesEdit propDisplacement = accessory.GetDrawPropertiesEdit(propFan.Owner,new(5, 4.75f),propFan.DestoryAt,false);
            propDisplacement.Offset = propFan.Offset;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, propDisplacement);

        }


        //第一组小怪螺旋尾35768(4.7s)提示
        //绘制思路为在事件的EffectPosition位置绘制一个圆圈
        [ScriptMethod(name: "小怪 1 螺旋尾 Mob 1 Tail Screw Dangerous Zone Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35768|35785)$"])]
        public void Mob1_TailScrewDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"Actived! => Mob 1 Tail Screw Dangerous Zone Draw");
            accessory.FastDraw(DrawTypeEnum.Circle,@event.GetEffectPosition(),new(4.1f),new (0,4500),false);
        }

        //第一组小怪泡泡吐息+蟹甲流提示
        //泡泡吐息+蟹甲流为连续的二连AOE，以泡泡吐息的读条为开始信号。泡泡吐息技能ID为35769，读条时长为4.7s。蟹甲流技能ID为35770，读条时长为1.2s。
        [ScriptMethod(name: "小怪 1 螃蟹前后刀 Mob 1 Bubble Shower Dangerous Zone Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35769|35786)$"])]
        public void Mob1_BubbleShowerDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"Actived! => Mob 1 Bubble Shower Dangerous Zone Draw");
            //正面AOE
            accessory.FastDraw(DrawTypeEnum.Fan,@event.GetSourceId(),new Vector2(9.1f),new (0,4400),false);
            //背面AOE可以认为是5.9s的读条，在第3.5s时进行提示
            DrawPropertiesEdit propFanBack = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(6.1f),4300,false);
            propFanBack.Rotation = (float)(Math.PI);
            propFanBack.Radian = 2 * (float)(Math.PI) / 3;
            propFanBack.Delay = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, propFanBack);

        }




        //第一组小怪水化炮35773(4.7s)+驱逐35775(4.7s)+电漩涡35774(4.7s)<=三连,写在一块
        [ScriptMethod(name: "小怪 1 钢铁月环三连 Mob 1 Hydrocannon Dangerous Zone Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35773|35915)$"])]
        public void Mob1_HydrocannonDangerousZoneDraw(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"Actived! => Mob 1 Hydrocannon Dangerous Zone Draw");
            accessory.FastDraw(DrawTypeEnum.Rect,@event.GetSourceId(),new(6, 15),new (0,4500),false);

            DrawPropertiesEdit propCircle = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(8.1f),4500,false);
            propCircle.Delay = 6900;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, propCircle);

            DrawPropertiesEdit propDonut = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(30.1f),4500,false);
            propDonut.InnerScale = new(7.9f);
            propDonut.Radian = 2 * (float)(Math.PI);
            propDonut.Delay = 13800;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, propDonut);



        }

        //第一组小怪水球喷射35941(4.7s)击退方向提示
        //绘制思路,起点为targetPosition,终点为boss,旋转Pi,限制宽度1，限制长度7
        [ScriptMethod(name: "小怪 1 击退点名 Mob 1 Hydro Shot Repulse Direction Draw", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35941|35793)$"])]
        public void Mob1_HydroShotRepulseDirectionDraw(Event @event, ScriptAccessory accessory)
        {
            //accessory.Log.Debug($"Actived! => Mob 1 Hydro Shot Repulse Direction Draw");
            DrawPropertiesEdit propDisplacement = accessory.GetDrawPropertiesEdit(@event.GetTargetId(),new(1, 7),4500,false);
            propDisplacement.TargetPosition = @event.GetSourcePosition();
            propDisplacement.Rotation = (float)(Math.PI);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, propDisplacement);
            
        }
        #endregion

        #region TODO
        // 小怪1 风圈路径示意
        // boss1 钢铁月环预站位
        // boss1 愤怒之海分摊分撒范围示意
        // boss1 ID 需要修正的内容  泡泡的dataID,水墙的dataID,双马尾的dataID

        // boss2 减算爆雷b指路
        // boss2 减算爆雷b指路中关于4雷的优化 如果第一组十字魔纹后4雷的层数不为2,则向左引导多吃一层再去最终位置
        #endregion

        #region Boss1
        //BOSS模型移除，通常用于初始化信息
        [ScriptMethod(name: "Boss RemoveCombatant", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:9020"], userControl: false)]
        public async void BossCombatantInitialize(Event @event, ScriptAccessory accessory)
        {
            if (IsInSuppress(2000, nameof(BossCombatantInitialize))){
                return;
            };
            accessory.Log.Debug($"Actived! => Boss RemoveCombatant Initialize");

            //由于是Init方法中有清楚时间戳记录的操作,延迟1秒,确保其他的同时触发方法被排除
            await DelayMillisecond(1000);
            Init(accessory);
        }

        /*如何计算BOSS1第一次水晶的安全区;
        请注意,狒狒中的坐标表述为(a,b,c),其中b为z轴坐标,a为x轴,c为y轴;
        设Boss场地中心为原点O,靠近场地中心的水晶所在的坐标为P0(x,y);
        实际测试中，原点O在游戏内的坐标就是(0,0,0),x和y的绝对值是5;
        四个基础点位为P1(3x,3y),P2(3x,-y),P3(-x,-y),P4(-x,3y);
        根据水晶的模型，分为竖向增量(距离2y),增量符号为(int)Math.Round(-y/Math.Abs(y));
        或者横向增量(距离2x),增量符号为(int)Math.Round(-x/Math.Abs(x));
        增量后的点需要具备以下特点:1,离P0点的距离相对比增量前更远;2,横坐标相对原点O不大于3.5x,纵坐标相对于原点不大于3.5y;
        游戏内的思路:
        水晶刷新在地面时会释放无读条技能 衝擊35498,日志行中的SourcePosition为水晶的落点,当水晶落点横纵坐标绝对值都小于6,即判定该水晶为基准水晶;
        */
        [ScriptMethod(name: "Boss 1 水晶安全区 Boss 1 Spring Crystals Safe Zone Draw", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(35498|35546)$"])]
        public void Boss1_FirstSpringCrystalsSafeZoneDraw(Event @event, ScriptAccessory accessory)
        {
            Vector3 crystalPos = @event.GetSourcePosition();
            if(Math.Abs(crystalPos.X) > 6 || Math.Abs(crystalPos.Z) > 6 )
            {
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 1 Spring Crystals Safe Zone Draw");
            //模板为在狒坐标第一象限的 靠近中心的 横水晶
            Vector2 safePoint1_template = new (15,15);
            Vector2 safePoint2_template = new (15,-15);
            Vector2 safePoint3_template = new (-5,-15);
            Vector2 safePoint4_template = new (-5,15);

            /*
            1, 计算中心水晶坐标角度值
            2, 将水晶的面向0横水晶，-1.57纵水晶加入计算
            
            角度 π/4  , 横水晶, 不转;     纵水晶，原点转+π/2
            角度 3π/4 , 横水晶, 原点转π;  纵水晶，原点转π/2
            角度 5π/4 , 横水晶, 原点转π;  纵水晶，原点转3π/2
            角度 7π/4 , 横水晶, 原点转2π; 纵水晶, 原点转3π/2

            */

            double rad = 0 ;
            double crystalPosRad = MathF.Atan2(@event.GetSourcePosition().Z,@event.GetSourcePosition().X);
            crystalPosRad = crystalPosRad < 0 ? crystalPosRad + 2 * Math.PI : crystalPosRad ;
            if(Math.Abs(@event.GetSourceRotation() - 0) > 1){
                //说明是纵水晶
                rad = crystalPosRad < Math.PI ? Math.PI * 1f/2 : Math.PI * 3f/2 ;

            }
            else
            {
                rad = ((crystalPosRad > Math.PI * 1f/2)&&(crystalPosRad < Math.PI * 3f/2) ) ? Math.PI : 0; 
                //说明是横水晶
            }
            accessory.Log.Debug($"Boss 1 Spring Crystals Safe Zone : rotate rad => {rad}");

            List<Vector2> safePoints = new List<Vector2>();
            safePoints.Add(Util.RotatePoint(safePoint1_template,rad));
            safePoints.Add(Util.RotatePoint(safePoint2_template,rad));
            safePoints.Add(Util.RotatePoint(safePoint3_template,rad));
            safePoints.Add(Util.RotatePoint(safePoint4_template,rad));

            boss1_springCrystalsSafePoints.Clear();
            boss1_springCrystalsSafePoints.AddRange(safePoints);

            for (int i = 0; i < safePoints.Count; i++)
            {
                DrawPropertiesEdit propRect = accessory.GetDrawPropertiesEdit(
                    new Vector3(safePoints[i].X,@event.GetSourcePosition().Y,safePoints[i].Y)
                    ,new(10,10),31000,true);
                propRect.Offset = new(0, 0, 5);
                propRect.Delay = 2000;
                propRect.Color = accessory.Data.DefaultSafeColor.WithW(0.4f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, propRect);
            };

  

        }





        /*如何给第一次水晶分摊或者分散指定安全区;<=采用融合法
        泡泡buff指路,终点有两个,靠近boss或者少吹风;<=采用少吹风, 写起来比较简单
        止步buff指路,通过中心水晶的位置计算两个基准点;如果是分散就去最近的二麻安全区,如果是分摊就去最近的一麻安全区
        需要记录四个点，每个点为四角所在的四分之一场地的中心点;需要记录该四分之一场地的麻将序号(或者水晶的面向,0为一麻,-1.57为二麻)
        需要两个参数 1.buff类型 2.职能位置(MTH1D1D2)
        1.泡泡buff+MT/D2去北半场(z轴坐标小于0);泡泡buff+H1/D1去南半场(z轴坐标大于0);
        2.止步buff+MT/D2去靠近西北角的安全区;止步buff+H1/D2去靠近东南角的安全区;分摊去一麻区域，分散去二麻区域

        Boss1在完成泡泡debuff赋予+分摊分散buff赋予后,会读条 鲸尾突风35505(2.7s), 以该读条作为第一次水晶机制指路的开始点
        优先级 MT D2 D1 H1
        气泡网回声debuff=3743(泡泡), 气泡凝集debuff=3788(止步)
        选定目标水化弹debuff=3748(分散),选定目标水瀑debuff=3747(分摊)
        */


        [ScriptMethod(name: "Boss 1 水晶指路(融合法) Boss 1 Spring Crystals Guide", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:35505"])]
        public void Boss1_FirstSpringCrystalsGuide(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"Actived! => Boss 1 Spring Crystals Guide");
            if(IsInSuppress(2000, nameof(Boss1_FirstSpringCrystalsGuide)))
            {
                return;
            }

            //该机制的优先级
            //(MT=0,D2=4,D1=3,H1=1)
            int[] roleMarkPriority = new int[] { (int)RoleMarkEnum.MT, (int)RoleMarkEnum.D2, (int)RoleMarkEnum.D1, (int)RoleMarkEnum.H1 };

            //标记是否获得泡泡的debuff
            uint bubbleDebuffId = 3743;
            uint fetterDebuffId = 3788;
            bool isMeGetBubbleDebuff =accessory.isMeGetStatus(bubbleDebuffId);

            //标记是否获得分摊debuff
            uint hydrofallDebuffId = 3747;
            bool isMeGetHydrofallDebuff = accessory.isMeGetStatus(hydrofallDebuffId);
            bool isPartyGetHydrofallDebuff = accessory.whoGetStatusInParty(hydrofallDebuffId).Count > 0;

            //标记是否去西北半场处理机制
            bool isMeGoToNorthWest = false;

            //水晶模型ID
            uint springCrystalDataId1 = 16542;
            uint springCrystalDataId2 = 16549;

            //我的目的地
            //float[] myEndPosition = new float[] { 0, 0, 0 };
            Vector3 myEndPosition = new (0,0,0);

            if (isMeGetBubbleDebuff)
            {
                //我是泡泡debuff,泡泡玩家无视分摊分散
                //通常可以根据职能位置直接判断去哪个位置, 但是为了避免意外情况, 查找另外一个同样debuff的玩家, 是什么职能

                //1.查找另外一个泡泡玩家,结果可能为null
                List<uint> bubbleDebuffPlayers = accessory.whoGetStatusInPartyWithoutMe(bubbleDebuffId);
                //2.确定去哪
                if (bubbleDebuffPlayers.Count == 0)
                {
                    isMeGoToNorthWest = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex()) <= 1;
                }
                else
                {
                    isMeGoToNorthWest = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex())
                                        < Array.IndexOf(roleMarkPriority, accessory.GetIndexInParty(bubbleDebuffPlayers[0]));
                }
                //3.找到二麻水晶(-1.57)的对角
                
                IEnumerable<IGameObject> _springCrystals1 = accessory.GetEntitiesByDataId(springCrystalDataId1).Where(obj => obj!=null && obj.Rotation < - 0.1);
                IEnumerable<IGameObject> _springCrystals2 = accessory.GetEntitiesByDataId(springCrystalDataId2).Where(obj => obj!=null && obj.Rotation < - 0.1);
                
                IEnumerable<IGameObject> springCrystals = _springCrystals1.Union(_springCrystals2);
                Vector2 myIndexPos = isMeGoToNorthWest ? new (0,-10) : new (0,10);
                //通过坐标Z值将水晶进行排序
                springCrystals = isMeGoToNorthWest 
                        ? springCrystals.OrderBy(obj => (obj?.Position ?? new (100,100,100)).Z)
                        : springCrystals.OrderByDescending(obj => (obj?.Position ?? new (-100,-100,-100)).Z);
                if(springCrystals.Count() > 0){
                    //Vector3 springCrystalPos = springCrystals[0]?.Position??new(0,0,0) ;
                    Vector3 springCrystalPos = ((springCrystals.ToList())[0]?.Position) ?? new (0,0,0);
                    myEndPosition = Util.RotatePointInFFXIVCoordinate(springCrystalPos
                        ,new Vector3(Math.Sign(springCrystalPos.X) * 10, springCrystalPos.Y, Math.Sign(springCrystalPos.Z) * 10)
                        ,Math.PI);
                }
            }
            else
            {
                //我不是泡泡debuff, 通常会是止步debuff(也有可能是无buff)

                uint anotherOneId = 0;
                if (isPartyGetHydrofallDebuff)
                {
                    //我被点了分摊, 查找另外一个分摊玩家, 结果可能为null
                    //或者我没分摊，但是小队是分摊，要按照分摊处理

                    //另一个需要与其比较优先的玩家的小队序号

                    if (isMeGetHydrofallDebuff)
                    {
                        //我被点了分摊, 查找另外一个分摊玩家, 结果可能为null
                        List<uint> hydrofallDebuffPlayers = accessory.whoGetStatusInPartyWithoutMe(hydrofallDebuffId);
                        anotherOneId = hydrofallDebuffPlayers.Count > 0 ? hydrofallDebuffPlayers[0] : anotherOneId;
                    }
                    else
                    {
                        //我没分摊，但是小队是分摊，要按照分摊处理
                        //查找另外一个没分摊buff的玩家, 结果可能为null
                        List<uint> noHydrofallDebuffPlayers = accessory.whoNotGetStatusInPartyWithoutMe(hydrofallDebuffId);
                        anotherOneId = noHydrofallDebuffPlayers.Count > 0 ? noHydrofallDebuffPlayers[0] : anotherOneId;
                    }
                }
                else
                {
                    //小队全员无分摊debuff,且我不是泡泡debuff
                    //通常会有止步buff, 如果没有,可以随便站
                    List<uint> fetterDebuffPlayers = accessory.whoGetStatusInPartyWithoutMe(fetterDebuffId);
                    anotherOneId = fetterDebuffPlayers.Count > 0 ? fetterDebuffPlayers[0] : anotherOneId;
                }
                if (anotherOneId == 0)
                {
                    isMeGoToNorthWest = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex()) <= 1;
                }
                else
                {
                    isMeGoToNorthWest = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex())
                                    < Array.IndexOf(roleMarkPriority, accessory.GetIndexInParty(anotherOneId));
                }
                myEndPosition = isMeGoToNorthWest ? new(16,0,16) : new (-16,0,-16);
                //分摊要去在一麻区域的安全区, 分散要去二麻区域的安全区
                foreach(Vector2 safePoint in boss1_springCrystalsSafePoints){
                    //通过安全区，查找位于该安全区的水晶的面向，判断是一麻还是二麻
                    IEnumerable<IGameObject> _springCrystals1 = accessory.GetEntitiesByDataId(springCrystalDataId1).Where(obj => obj!=null && Math.Sign(obj.Position.X) == Math.Sign(safePoint.X) && Math.Sign(obj.Position.Z) == Math.Sign(safePoint.Y)) ;
                    IEnumerable<IGameObject> _springCrystals2 = accessory.GetEntitiesByDataId(springCrystalDataId2).Where(obj => obj!=null && Math.Sign(obj.Position.X) == Math.Sign(safePoint.X) && Math.Sign(obj.Position.Z) == Math.Sign(safePoint.Y)) ;
                    IEnumerable<IGameObject> springCrystals = _springCrystals1.Union(_springCrystals2);
                    // IEnumerable<IGameObject> springCrystals = accessory.GetEntitiesByDataId(springCrystalDataId1)
                    //     .Where(obj => obj != null 
                    //     && Math.Sign(obj.Position.X) == Math.Sign(safePoint.X)
                    //     && Math.Sign(obj.Position.Z) == Math.Sign(safePoint.Y));
                    //accessory.Log.Debug($"Boss 1 First Spring Crystals Guide : springCrystals.Count() => {springCrystals.Count()}");
                   
                    if(springCrystals.Count() > 0 && (Math.Sign(Math.Round((springCrystals.ToList())[0].Rotation)) == (isPartyGetHydrofallDebuff ? 0 : -1)))
                    {   
                        //accessory.Log.Debug($"Boss 1 Spring Crystals Guide : springCrystals[0] => {(springCrystals.ToList())[0].Position},{(springCrystals.ToList())[0].Rotation}");
                        //西北玩家的点应该更加靠近 -16,0,-16 ,如果当前安全区的点比myEndPosition更靠近,则录入
                        Vector3 startPoint = isMeGoToNorthWest ? new( -16, 0, -16 ) : new ( 16, 0, 16 );
                        myEndPosition = Math.Sqrt(Math.Pow(safePoint.X - startPoint.X, 2) + Math.Pow(safePoint.Y - startPoint.Z, 2))
                                        < Math.Sqrt(Math.Pow(myEndPosition.X - startPoint.X, 2) + Math.Pow(myEndPosition.Z - startPoint.Z, 2))
                                        ? new (safePoint.X,0,safePoint.Y)
                                        :myEndPosition;
                    }
                    

                
                    
                }
                
            }
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : isMeGetBubbleDebuff => {isMeGetBubbleDebuff}");
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : isPartyGetHydrofallDebuff => {isPartyGetHydrofallDebuff}");
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : isMeGetHydrofallDebuff => {isMeGetHydrofallDebuff}");
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : isMeGoToNorthWest => {isMeGoToNorthWest}");
            accessory.Log.Debug($"Boss 1 Spring Crystals Guide : myEndPosition => ( {myEndPosition.X},{myEndPosition.Y},{myEndPosition.Z} )");
            MultiDisDraw(new List<float[]> { new float[5] { myEndPosition.X, myEndPosition.Y, myEndPosition.Z, 0, 12000 } }, accessory);
        }



        //为吹气泡机制判断泡泡类型,同时画半圆
        [ScriptMethod(name: "Boss 1 小泡泡 Boss 1 Blowing Bubbles Type", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16544|16551)$"], userControl: false)]
        public void Boss1_BlowingBubblesType(Event @event, ScriptAccessory accessory)
        {
            //如果出现(-20,0,-17.5)的泡泡，则为顺时针
            Vector3 bubblePos = @event.GetSourcePosition();
            lock (_lock){
                boss1_bubbleIsClockwise = boss1_bubbleIsClockwise || ((Math.Abs(bubblePos.X - (-20)) < 0.1) && (Math.Abs(bubblePos.Z - (-17.5)) < 0.1));
            }
            
            DrawPropertiesEdit propFan = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(2.55f),16000,false);
            propFan.Offset = new(0, 0, -1.3f);
            propFan.Radian = (float)(Math.PI);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, propFan);

            DrawPropertiesEdit propDisplacement = accessory.GetDrawPropertiesEdit(propFan.Owner,new(2, 2.3f),propFan.DestoryAt,false);
            propDisplacement.Offset = new(0, 0, -1.3f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, propDisplacement);

        }
        /*
        吹气泡+分摊分散指路
        吹气泡有两种情况， 通过检查最靠近右下角(20,0,20)的泡泡. 如果x<z, 则为逆时针路线. 如果x>z则为顺时针路线
        每个人的起点为每个四分之一场地, 靠近场中的点
        三个水圈(直径10),第三个水圈放下时buff1触发; 短暂延迟后下一组的第一个水圈,在放下第二个水圈的同时,buff2触发
        Boos1在完成分摊分散buff赋予+召唤泡泡后, boss本体会开始读条水化爆弹35536(1.9s), 以该读条作为吹气泡+分摊分散指路的开始点
        */
        [ScriptMethod(name: "Boss 1 小泡泡+分摊分散指路 Boss 1 Blowing Bubbles Guide", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35536|35489)$"])]
        public void Boss1_BlowingBubblesGuide(Event @event, ScriptAccessory accessory)
        {

            if(IsInSuppress(60000, nameof(Boss1_BlowingBubblesGuide)))
            {
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 1 Blowing Bubbles Guide ");
            //分摊DebuffId
            uint hydrofallDebuffId = 3747;
            uint hydrobombDebuffId = 3748;
            //优先级
            int[] roleMarkPriority = new int[] { (int)RoleMarkEnum.MT, (int)RoleMarkEnum.H1, (int)RoleMarkEnum.D1, (int)RoleMarkEnum.D2 };

            //判断顺时针(分摊后横向走)    还是逆时针(分摊后竖向走)
            bool isClockwise = boss1_bubbleIsClockwise;
            //查找漂浮水泡，漂浮水泡的DataId为16544

            //判断先分摊     还是先分散; 我是否获得分摊debuff
            bool isHydrofallFirst = false;
            bool isMeGetHydrofallDebuff = accessory.isMeGetStatus(hydrofallDebuffId);

            //通常分摊会点两个Dps或者(MT+H1), 但是当出现意外情况时(比如MT+D2 或者 H1+D1 时),分摊时需要把D2与D1换位(也可以MT和H1换位)
            bool isHydrofallDebuffNeedChange = false;
            List<uint> hydrofallDebuffPlayers = accessory.whoGetStatusInParty(hydrofallDebuffId);
            List<uint> hydrobombDebuffPlayers = accessory.whoGetStatusInParty(hydrobombDebuffId);

            float hydrofallDebuffTime = hydrofallDebuffPlayers.Count > 0 ? accessory.GetStatusInfo(hydrofallDebuffPlayers[0], hydrofallDebuffId).RemainingTime : -1;
            float hydrobombDebuffTime = hydrobombDebuffPlayers.Count > 0 ? accessory.GetStatusInfo(hydrobombDebuffPlayers[0], hydrobombDebuffId).RemainingTime : -1;
            isHydrofallFirst = hydrofallDebuffTime < hydrobombDebuffTime;
            isHydrofallDebuffNeedChange = (hydrofallDebuffPlayers.Count == 2) && (accessory.GetIndexInParty(hydrofallDebuffPlayers[0]) + accessory.GetIndexInParty(hydrofallDebuffPlayers[1]) == 3);


            //设计路径
            //以狒狒坐标系中第一象限作为图样象限,假设起点是(5f,0,5f)
            Vector2 startPoint_template = new(5f, 5f);
            Vector2 point2_template = isClockwise ? new(15f, 5f) : new(5f, 15f);
            Vector2 point3_template = new(15f, 15f);

            // Vector2 point4_template = new (0,0);
            // Vector2 point5_template = new (0,0);
            // Vector2 endPoint_template = new (0,0);

            //先分摊后分散,需要前往分散点的玩家走位
            Vector2[] meGo = new Vector2[]{
                isClockwise ? new (1.5f,11.5f) : new (11.5f,1.5f),
                isClockwise ? new (0,6.5f) : new (6.5f,0),
                isClockwise ? new (-18.5f,8.5f) : new (8.5f,-18.5f),
            };
            Vector2[] meStay = new Vector2[]{
                isClockwise ? new (18f,18f) : new (18f,18f),
                isClockwise ? new (8.5f,19f) : new (19f,8.5f),
                new (9f,9f),
            };
            Vector2[] notHydrofallFirst = new Vector2[]{
                isClockwise ? new (6f,11f) :new (11f,6f),
                //isClockwise ? new (6f,10f) :new (10f,6f),
                new (6f,0),
                new (15f,0)
            };


            /* 设计节点路径
            点1,起始点;(等待放下第一个水圈)
            点2,起始点围绕四分一场地转π/2(方向由isClockwise决定);(等待放下第二个水圈)
            点3,起始点围绕四分一场地转π;(等待放下第三个水圈,放下第三个水圈的同时第一个buff触发)
            点4,线段距离终点6m的一个点(等待放下第四个水圈) <= 先分散后分摊
            点4,点3与终点的"中点"再向场地中心移动5m <= 先分摊后分散
            点5,终点(等待放下第五个水圈，放下第五个水圈的同时第二个buff触发)
            剩下还有一个水圈，躲避即可
            */
            int myPriority = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex());

            //如果出现了意外情况,两个分摊点到了同一组,需要把两个DPS换一下

            myPriority = (isHydrofallDebuffNeedChange && (myPriority == 2 || myPriority == 3)) ? (5 - myPriority) : myPriority;
            float radian = (float)(Math.PI * 0.5 * (myPriority - 2));

            Vector2 originPoint = new(0, 0);
            Vector2 myStartPoint = Util.RotatePoint(startPoint_template, radian);
            Vector2 myPoint2 = Util.RotatePoint(point2_template, radian);
            Vector2 myPoint3 = Util.RotatePoint(point3_template, radian);
            Vector2 myPoint4 = new(0, 0);
            Vector2 myPoint5 = new(0, 0);
            Vector2 myEndPoint = new(0, 0);

            if (!isHydrofallFirst)
            {
                // 先分散 后分摊
                myPoint4 = Util.RotatePoint(notHydrofallFirst[0], radian);
                myPoint5 = Util.RotatePoint(notHydrofallFirst[1], (myPriority == 0 || myPriority == 3) ?  Math.PI : 0);
                myEndPoint = Util.RotatePoint(notHydrofallFirst[2], (myPriority == 0 || myPriority == 3) ?  Math.PI : 0);
            }
            else
            {
                //先分摊 后分散
                switch (myPriority)
                {
                    case 0:
                    case 2:
                        myPoint4 = Util.RotatePoint(meGo[0], radian);
                        myPoint5 = Util.RotatePoint(meGo[1], radian);
                        myEndPoint = Util.RotatePoint(meGo[2], radian);
                        break;
                    case 1:
                    case 3:
                        radian += (float)(0.5 * Math.PI);
                        myStartPoint = Util.RotatePoint(startPoint_template, radian);
                        myPoint2 = Util.RotatePoint(point2_template, radian);
                        myPoint3 = Util.RotatePoint(point3_template, radian);
                        myPoint4 = Util.RotatePoint(meStay[0], radian);
                        myPoint5 = Util.RotatePoint(meStay[1], radian);
                        myEndPoint = Util.RotatePoint(meStay[2], radian);
                        break;
                }

            }


            List<float[]> displacementsPointsList = new List<float[]>();
            displacementsPointsList.Add(new float[] { myStartPoint.X, 0, myStartPoint.Y, 0, 3600 });
            displacementsPointsList.Add(new float[] { myPoint2.X, 0, myPoint2.Y, 0, 2000 });
            displacementsPointsList.Add(new float[] { myPoint3.X, 0, myPoint3.Y, 0, 2000 });
            displacementsPointsList.Add(new float[] { myPoint4.X, 0, myPoint4.Y, 0, 3700 });
            displacementsPointsList.Add(new float[] { myPoint5.X, 0, myPoint5.Y, 0, 1800 });
            displacementsPointsList.Add(new float[] { myEndPoint.X, 0, myEndPoint.Y, 0, 2000 });

            //微调一下先分摊后分散+meGo时，最后两个指路点位的的时间间隔
            if (isHydrofallFirst && (myPriority == 2 || myPriority == 0))
            {
                displacementsPointsList[3][4] = 2400;
                displacementsPointsList[4][4] = 1300;
                displacementsPointsList[5][4] = 2500;
            }

            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : isClockwise => {isClockwise}");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : isHydrofallFirst => {isHydrofallFirst}");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : isMeGetHydrofallDebuff => {isMeGetHydrofallDebuff}");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : isHydrofallDebuffNeedChange => {isHydrofallDebuffNeedChange}");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point1(start) => ({Math.Round(displacementsPointsList[0][0])},{Math.Round(displacementsPointsList[0][1])},{Math.Round(displacementsPointsList[0][2])})");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point2 => ({Math.Round(displacementsPointsList[1][0])},{Math.Round(displacementsPointsList[1][1])},{Math.Round(displacementsPointsList[1][2])})");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point3(first debuff) => ({Math.Round(displacementsPointsList[2][0])},{Math.Round(displacementsPointsList[2][1])},{Math.Round(displacementsPointsList[2][2])})");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point4 => ({Math.Round(displacementsPointsList[3][0])},{Math.Round(displacementsPointsList[3][1])},{Math.Round(displacementsPointsList[3][2])})");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point5 => ({Math.Round(displacementsPointsList[4][0])},{Math.Round(displacementsPointsList[4][1])},{Math.Round(displacementsPointsList[4][2])})");
            accessory.Log.Debug($"Boss 1 Blowing Bubbles Guide : point6(end) => ({Math.Round(displacementsPointsList[5][0])},{Math.Round(displacementsPointsList[5][1])},{Math.Round(displacementsPointsList[5][2])})");


            MultiDisDraw(displacementsPointsList,accessory);

        }



        //为钢铁月环标记水墙的危险区
        [ScriptMethod(name: "Boss 1 水墙 Boss 1 Twintides Bubble Type", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2013494", "Operate:Add"], userControl: false)]
        public void Boss1_TwintidesBubbleType(Event @event, ScriptAccessory accessory)
        {
            Vector3 bubblePos = @event.GetSourcePosition();
            if (((Math.Abs(bubblePos.Z - (-20)) < 0.1) && Math.Abs(Math.Abs(bubblePos.X) - 5) < 0.1)
                ||((Math.Abs(bubblePos.X - (-20)) < 0.1) && Math.Abs(Math.Abs(bubblePos.Z) - 5) < 0.1))
            {
                //南北情况
                //将纵坐标为-20,横坐标5或者-5的的泡泡存放进boss1_twintidesBubbleType

                //东西情况
                //将横坐标为-20,纵坐标5或者-5的的泡泡存放进boss1_twintidesBubbleType


                boss1_twintidesBubbleType = bubblePos;
                //最终存放的应该是后添加的泡泡
            }
            
            DrawPropertiesEdit propRect = accessory.GetDrawPropertiesEdit(@event.GetSourceId(),new(10, 20),6600-1400,false);
            //propRect.ScaleMode = ScaleMode.ByTime;
            propRect.Delay = 4000 + 1400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, propRect);
        }

        /*
        圆浪连潮35532(5.0s) 环浪连潮35534(5.0s)
        Boss1钢铁月环+分摊指路
        MTD2D1H2,按照优先级分配到A或者C
        需要确定先钢铁还是先月环
        需要确定是左穿右还是右穿左
        三个点
        1.起点, 躲避第一段钢铁月环+第一段水墙
        2.拐点, 纵向移动躲避第二段钢铁月环
        3.终点, 横向移动躲避第二段水墙
        */

        [ScriptMethod(name: "Boss 1 钢铁月环指路 Boss 1 Twintides Guide", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35532|35534|35559|35561)$"])]
        public void Boss1_TwintidesGuide(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"Actived! => Boss 1 Twintides Guide");

            //是先钢铁吗
            int recedingTwintidesActionId1 = 35532;
            int recedingTwintidesActionId2 = 35559;
            bool isRecedingTwintides = (@event.GetActionId() == recedingTwintidesActionId1) || (@event.GetActionId() == recedingTwintidesActionId2);


            //判断水墙类型+哪一侧先
            bool isVerticalWater = Math.Abs(boss1_twintidesBubbleType.Z - (-20)) < 0.1 ;
            bool isNorth_leftWaterFirst = isVerticalWater ? boss1_twintidesBubbleType.X > 4 : boss1_twintidesBubbleType.Z < -4;

            //分摊DebuffId
            uint hydrofallDebuffId = 3747;


            int[] roleMarkPriority = new int[] { (int)RoleMarkEnum.MT, (int)RoleMarkEnum.D2, (int)RoleMarkEnum.D1, (int)RoleMarkEnum.H1 };
            //1,确定自己应该去A还是去C
            //2,确定起始点的横坐标
            //3,确定起始点的纵坐标绝对值为 15或者5

            //我是分摊点名
            bool isMeGoToNorth = false;
            bool isMeGetHydrofallDebuff = accessory.isMeGetStatus(hydrofallDebuffId);
            uint anotherOneId = 0;

            List<uint> players = isMeGetHydrofallDebuff 
                                    ?accessory.whoGetStatusInPartyWithoutMe(hydrofallDebuffId)
                                    :accessory.whoNotGetStatusInPartyWithoutMe(hydrofallDebuffId);

            anotherOneId = players.Count > 0 ? players[0] : anotherOneId;
        
            isMeGoToNorth = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex())
                            < (anotherOneId == 0 ? 2 : Array.IndexOf(roleMarkPriority,accessory.GetIndexInParty(anotherOneId)));



            //假设图样的起始点在C一侧
            Vector2 originPoint = new(0, 0);
            Vector2 startPoint_template = new((isNorth_leftWaterFirst?1:-1) * (-2), isRecedingTwintides ? 15f : 5f);
            Vector2 point2_template = new((isNorth_leftWaterFirst?1:-1) * (-1), 10f);
            Vector2 endPoint_template = new((isNorth_leftWaterFirst?1:-1) * 2, isRecedingTwintides ? 5f : 15f);


            Vector2 mySatrtPoint = Util.RotatePoint(startPoint_template, isMeGoToNorth ? Math.PI : 0);
            Vector2 myPoint2 = Util.RotatePoint(point2_template, isMeGoToNorth ? Math.PI : 0);
            Vector2 myEndPoint = Util.RotatePoint(endPoint_template, isMeGoToNorth ? Math.PI : 0);

            //如果是东西侧，需要再旋转-0.5π
            if(!isVerticalWater)
            {
                mySatrtPoint = Util.RotatePoint(mySatrtPoint,-0.5 * Math.PI);
                myPoint2 = Util.RotatePoint(myPoint2,-0.5 * Math.PI);
                myEndPoint = Util.RotatePoint(myEndPoint,-0.5 * Math.PI);
            }

            List<float[]> displacementsPointsList = new List<float[]>();
            displacementsPointsList.Add(new float[] { mySatrtPoint.X, 0, mySatrtPoint.Y, 0, 4600 });
            displacementsPointsList.Add(new float[] { myPoint2.X, 0, myPoint2.Y, 0, 1000 });
            displacementsPointsList.Add(new float[] { myEndPoint.X, 0, myEndPoint.Y, 0, 2000 });
            MultiDisDraw(displacementsPointsList, accessory);
            
            //画危险区
            DrawPropertiesEdit dpCircle = accessory.GetDrawPropertiesEdit(@event.GetSourceId(), new (14f), 1000, false);
            DrawPropertiesEdit dpDonut = accessory.GetDrawPropertiesEdit(@event.GetSourceId(), new (30f), 1000, false);
            dpDonut.InnerScale = new (7f);
            dpDonut.Radian = 2 * MathF.PI;
            if(isRecedingTwintides){
                //先钢铁
                dpCircle.DestoryAt = 4300;
                dpDonut.Delay = 5000;
                dpDonut.DestoryAt = 2400;
            }else{
                //先月环
                dpDonut.DestoryAt = 4300;
                dpCircle.Delay = 5000;
                dpCircle.DestoryAt = 2400;
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dpDonut);


            accessory.Log.Debug($"Boss 1 Twintides Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Twintides Guide : isRecedingTwintides => {isRecedingTwintides}");
            accessory.Log.Debug($"Boss 1 Twintides Guide : isNorth_leftWaterFirst => {isNorth_leftWaterFirst}");
            accessory.Log.Debug($"Boss 1 Twintides Guide : isMeGoToNorth => {isMeGoToNorth}");
            accessory.Log.Debug($"Boss 1 Twintides Guide : isMeGetHydrofallDebuff => {isMeGetHydrofallDebuff}");
            

        }


        /*
        第二次涌水水晶+小怪引导
        BOSS1在召唤了水晶+小怪后，会读条捕食气泡网35525(3.8s) 该读条特殊与前两次捕食气泡网不同ID ，在短暂的延迟后，赋予场上两名玩家和两个小怪 泡泡debuff
        按照优先级，止步debuff玩家引导泡泡小怪，泡泡debuff玩家引导无buff小怪
        (或者使用咆哮35524作为开始标记)
        */
        [ScriptMethod(name: "Boss 1 小怪面向引导 Boss 1 Mob Guide", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35525|35575)$"])]
        public async void Boss1_MobGuide(Event @event, ScriptAccessory accessory)
        {
            //Boss1整个战斗会多次读条捕食气泡网，需要做标识
            //++boss1_bubbleNetCastingCount != 2 || 
            if(await DelayMillisecond(5500)){
                //短暂延迟,确保debuff被赋予完成
                return;
            }
            boss1_phaseAfterMob = true;
            accessory.Log.Debug($"Actived! => Boss 1 Mob Guide");

            uint bubbleDebuffId = 3743;
            uint fetterDebuffId = 3788;
            int[] roleMarkPriority = new int[] { (int)RoleMarkEnum.MT, (int)RoleMarkEnum.H1, (int)RoleMarkEnum.D1, (int)RoleMarkEnum.D2 };


            //1.查找自身debuff情况并比对优先级高低
            bool isMeGetBubbleDebuff = accessory.isMeGetStatus(bubbleDebuffId);
            bool isMeGetFetterDebuff = accessory.isMeGetStatus(fetterDebuffId);
            uint myDebuffId = isMeGetBubbleDebuff ? bubbleDebuffId : (isMeGetFetterDebuff ? fetterDebuffId : 0);


            List<uint> players = new List<uint>();
            if(myDebuffId == 0){
                players = accessory.whoNotGetStatusInPartyWithoutMe(bubbleDebuffId).Intersect(
                    accessory.whoNotGetStatusInPartyWithoutMe(fetterDebuffId)).ToList();
            }else{
                players = accessory.whoGetStatusInPartyWithoutMe(myDebuffId);
            }
            uint anotherOneId = players.Count > 0 ? players[0] : 0 ;
            int myPriority = Array.IndexOf(roleMarkPriority, accessory.GetMyIndex());
            bool isMeFirst = (anotherOneId != 0)
                                ? myPriority < Array.IndexOf(roleMarkPriority, accessory.GetIndexInParty(anotherOneId))
                                : false;

            //2.查找小怪的debuff情况 , 根据旋转角度排序, - 0.5 π 为第一个 小怪DataId为16545,小怪身上的泡泡DebuffId为3745
            uint mobDataId1 = 16545;
            uint mobDataId2 = 16552;
            uint bubbleDebuffIdOnMob =3745;
            IEnumerable<IGameObject> _mobs1 = accessory.GetEntitiesByDataId(mobDataId1);
            IEnumerable<IGameObject> _mobs2 = accessory.GetEntitiesByDataId(mobDataId2);
            IEnumerable<IGameObject> mobs = _mobs1.Union(_mobs2);
            List<uint> mobIdsGetbubbleDebuff = accessory.whoGetStatus(bubbleDebuffIdOnMob);
            IEnumerable<IGameObject> mobsGetbubbleDebuff = mobs.Where(obj => obj is IGameObject gameObject && mobIdsGetbubbleDebuff.Contains(gameObject.EntityId));

            List<Vector3> mobsIndex = myDebuffId == bubbleDebuffId 
                                        ? mobs.Except(mobsGetbubbleDebuff).Select(obj => (obj?.Position) ?? new (0,0,0)).ToList()
                                        : mobsGetbubbleDebuff.Select(obj => (obj?.Position) ?? new (0,0,0)).ToList();
            mobsIndex = mobsIndex.OrderBy(v3 => (Math.Round(MathF.Atan2(v3.Z,v3.X) + 0.5* Math.PI) < 0 ? MathF.Atan2(v3.Z,v3.X) + 0.5* Math.PI : MathF.Atan2(v3.Z,v3.X) + 0.5* Math.PI)).ToList();
            //3.确定自己要引导哪个小怪，获得该先去哪个数字点处理分散
            Vector3 myMob = mobsIndex.Count > 0 
                            ? (isMeFirst ? mobsIndex[0] : mobsIndex[mobsIndex.Count -1])
                            : new (0,0,0) ;

            //4.查找水晶位置，计算安全区类型
            //以rad = 0 的位置作为安全点模板
            Vector2 startPoint_template = new (10,-10);
            //水晶模型ID
            uint springCrystalDataId1 = 16542;
            uint springCrystalDataId2 = 16549;
            //安全区标记 0,未检测到; 1左右，2上下,4四角
            int safeZoneType = 0;
            IEnumerable<IGameObject> _crystalList1 = accessory.GetEntitiesByDataId(springCrystalDataId1)
                                                .Where(obj => obj is IGameObject gameObject && gameObject.Position.X > 0 && gameObject.Position.Z < 0);
            IEnumerable<IGameObject> _crystalList2 = accessory.GetEntitiesByDataId(springCrystalDataId2)
                                                .Where(obj => obj is IGameObject gameObject && gameObject.Position.X > 0 && gameObject.Position.Z < 0);
                                                
                                                
            List<IGameObject> crystalList = _crystalList1.Union(_crystalList1).ToList();
            //Vector2 startPoint_template = new (0,0);
            if(crystalList.Count > 0 && Math.Round(crystalList[0].Rotation) < 0){
                //右上角水晶为竖水晶,左右安全区
                startPoint_template = new (11f, 9f);
                safeZoneType = 1;
            }else{
                //右上角水晶为横水晶
                //横坐标<10 四角安全区;否则上下安全区
                startPoint_template = crystalList[0].Position.X < 10 ? new (11f, 11f) : new (9f, 11f);
                safeZoneType = crystalList[0].Position.X < 10 ? 4 : 2;
            }
            Vector2 myStartPoint = startPoint_template;
            if(myMob.X < - 5)
            {
                myStartPoint  = Util.RotatePoint(startPoint_template, Math.PI);
            }
            else if(myMob.X < 5 && myMob.X >-5)
            {
                myStartPoint = myMob.Z > 0 ? Util.AxisymmetricPoint(startPoint_template,0.5 * Math.PI) : Util.AxisymmetricPoint(startPoint_template,0);
            }
            List<float[]> pointsList = new List<float[]>();
            pointsList.Add(new float[]{myStartPoint.X,myMob.Y,myStartPoint.Y,0,11100});
            pointsList.Add(new float[]{1.15f * myMob.X,myMob.Y,1.20f * myMob.Z,0,4000});
            MultiDisDraw(pointsList,accessory);

            //画小怪扇形引导
            foreach(IGameObject? mobObject in mobs)
            {
                if(mobObject is IGameObject mobIGameObject){
                    DrawPropertiesEdit dpFan = accessory.GetDrawPropertiesEdit(mobIGameObject.EntityId, new(4f), 8300 - 900, false);
                    dpFan.Delay = 10000 + 900;
                    dpFan.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dpFan.Radian = MathF.PI;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpFan);
                }
            }
            //画水晶安全区
            Vector4 safeZoneColor = accessory.Data.DefaultSafeColor.WithW(0.3f);
            switch(safeZoneType)
            {
                case 1 :
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(-15,0,-10),new (10,20),new (0,10500),safeZoneColor);
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(15,0,-10),new (10,20),new (0,10500),safeZoneColor);
                    break;
                case 2 : 
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(0,0,-20),new (20,10),new (0,10500),safeZoneColor);
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(0,0,10),new (20,10),new (0,10500),safeZoneColor);                  
                    break;
                case 4 : 
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(-15,0,-20),new (10,10),new (0,10500),safeZoneColor);
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(-15,0,10),new (10,10),new (0,10500),safeZoneColor);
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(15,0,-20),new (10,10),new (0,10500),safeZoneColor);
                    accessory.FastDraw(DrawTypeEnum.Rect,new Vector3(15,0,10),new (10,10),new (0,10500),safeZoneColor);                    
                    break;
                case 0 : 
                default:
                    break;
            }

            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : isMeGetBubbleDebuff => {isMeGetBubbleDebuff}");
            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : isMeGetFetterDebuff => {isMeGetFetterDebuff}");
            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : isMeFirst(high priority) => {isMeFirst}");
            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : safeZoneType(1←→,2↑↓) => {safeZoneType}");
            accessory.Log.Debug($"Boss 1 Second Spring Crystals Safe Zone Draw : myMob => {myMob}");
        }

        /*
        摩西开海方法，开海后会读条一次捕食气泡网召唤泡泡，以该读条作为开始的标记。
        场地的一侧会刷新4个水晶(圆水晶dataId为16541)，2个较靠近y轴，2个较远离y轴且横坐标的绝对值为15。该场地记为A侧
        关注2个较靠近y轴，其中一个纵坐标为0，另一个水晶的对侧，会有一个A侧DPS要进去的泡泡，该泡泡记为泡泡B
        给小泡泡和水晶画危险圆圈
        刷新4个塔，一个塔在A侧，另外三个塔在A的对侧

        假设A侧为场地的东侧，则塔(半径4)的坐标为西侧:(-10,-15),(-10,15),(-14,0); 东侧(14,10)
        MT的行为(-19,0)=>(-14，0)，H1的行为(6,0)=>(14,0)
        D1为进入泡泡B
        D2的起点和泡泡B的纵坐标符号有关，该符号记为C(值为1或者-1);
        D2行为(-19, -C* 15)=>(-10, -C* 15)
        后续还有一次水墙+钢铁分摊分散
        */

        /*
        愤怒之海35520 35523 35553读条,分割场地，为分摊分散标记终点和危险范围,
        TODO 重新配置开始的时间点，如果以愤怒之海为起始点，是否会导致前往安全区的指示时间不够
        以第一个buff赋予的作为判断的时机点，并且绘图，由于会有多次赋予buff的机制,引入 boss1_phaseAfterMob 参数来作为是否启用该触发器的标识
        */
        [ScriptMethod(name: "Boss 1 愤怒之海 水炮指路 Boss1 Angry Seas Hydrop Guide", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3747|3748)$"])]
        public async void Boss1_AngrySeasGuideFirst(Event @event, ScriptAccessory accessory)
        {
            if(IsInSuppress(5000, nameof(Boss1_AngrySeasGuideFirst)) || !boss1_phaseAfterMob)
            {
                return;
            }
            if(await DelayMillisecond(3000)){
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 1 Angry Seas Hydrop Guide");
            //分摊分散DebuffId
            boss1_phaseAfterMob = false;
            uint hydrofallDebuffId = 3747;
            uint hydrobombDebuffId = 3748;
            bool ishydrofallFirst = @event.GetStatusID() == hydrofallDebuffId;
            bool ishydrofallSameGroup = false;
            List<uint> hydrofallDebuffPlayers = accessory.whoGetStatusInParty(hydrofallDebuffId);
            ishydrofallSameGroup = hydrofallDebuffPlayers.Count == 2 && (accessory.GetIndexInParty(hydrofallDebuffPlayers[0]) + accessory.GetIndexInParty(hydrofallDebuffPlayers[1]) == 3);

            //判断4小怪后第一个赋予的buff是什么，就可以判断是分摊还是分散
            //如果分摊buff点在同一组两个DPS交换


            // 分摊的点位 MT,H1,D1,D2
            Vector2[] hydrofallDisperse = new Vector2[] {
                new (-10,0),
                new (10,0),
                ishydrofallSameGroup ?new (-10,0) :new (10,0),
                ishydrofallSameGroup ?new (10,0) :new (-10,0),
            };

            //分散的散开点 MT,H1,D1,D2
            Vector2[] hydrobombDisperse = new Vector2[] {
                new (-7,-7),
                new (15,-15),
                ishydrofallSameGroup ?new (-15,15) :new (7,7),
                ishydrofallSameGroup ?new (7,7) :new (-15,15),
            };


            

            long delay_firstMech = 0;
            long dispaly_firstMech = 8000;
            long delay_secondMech = 0;
            long dispaly_secondMech = 5000;

            Vector2 myStartPoint = ishydrofallFirst ? hydrofallDisperse[accessory.GetMyIndex()] : hydrobombDisperse[accessory.GetMyIndex()] ;
            Vector2 myEndPoint = ishydrofallFirst ? hydrobombDisperse[accessory.GetMyIndex()] : hydrofallDisperse[accessory.GetMyIndex()] ;


            //画指路
            List<float[]> pointsList = new List<float[]>();
            pointsList.Add(new float[]{myStartPoint.X, 0 , myStartPoint.Y , delay_firstMech ,dispaly_firstMech});
            pointsList.Add(new float[]{myEndPoint.X, 0 , myEndPoint.Y , delay_secondMech ,dispaly_secondMech});
            MultiDisDraw(pointsList,accessory);


            accessory.Log.Debug($"Boss 1 Angry Seas Hydrop Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Angry Seas Hydrop Guide : ishydrofallFirst => {ishydrofallFirst}");
            accessory.Log.Debug($"Boss 1 Angry Seas Hydrop Guide : ishydrofallSameGroup=> {ishydrofallSameGroup}");
            accessory.Log.Debug($"Boss 1 Angry Seas Hydrop Guide : myStartPoint => {myStartPoint}");
            accessory.Log.Debug($"Boss 1 Angry Seas Hydrop Guide : myEndPoint => {myEndPoint}");

            







        }

        [ScriptMethod(name: "Boss 1 愤怒之海 水晶踩塔指路 Boss 1 Angry Seas Crystals Guide", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16541|16548)$"])]
        public void Boss1_AngrySeasGuideSecond(Event @event, ScriptAccessory accessory)
        {
            Vector3 pos = @event.GetSourcePosition();
            bool isNearY = Math.Abs(pos.X) < 12;
            bool isAwayX = Math.Abs(pos.Z) > 10;
            if(!isNearY || !isAwayX)
            {
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 1 Angry Seas Crystals Guide");
            bool isCrystalOnEastSide = pos.X > 0;
            bool isCrystalOnNorthSide = pos.Z < 0;

            Vector3 crystalStay1 = new (isCrystalOnEastSide?15:-15,pos.Y,isCrystalOnNorthSide?-5:5);
            Vector3 crystalStay2 = new (isCrystalOnEastSide?15:-15,pos.Y,isCrystalOnNorthSide?15:-15);
            Vector3 crystalMove1 = new (isCrystalOnEastSide?-10:10,pos.Y,isCrystalOnNorthSide?-15:15);
            Vector3 crystalMove2 = new (isCrystalOnEastSide?-10:10,pos.Y,0);

            Vector3 startPoint_MT = new (isCrystalOnEastSide?-19:-8,pos.Y,0);
            Vector3 endPoint_MT = new (-14,pos.Y,0);

            Vector3 startPoint_H1 = new (isCrystalOnEastSide?8:19,pos.Y,0);
            Vector3 endPoint_H1 = new (14,pos.Y,0);

            Vector3 startPoint_D1 = isCrystalOnEastSide 
                            ? isCrystalOnNorthSide ? new (7.5f,pos.Y,12.5f) : new (7.5f,pos.Y,-12.5f)
                            : isCrystalOnNorthSide ? new (19,pos.Y,-15) : new (19,pos.Y,15);
            Vector3 endPoint_D1 = isCrystalOnEastSide
                            ? isCrystalOnNorthSide ? new (7.5f,pos.Y,12.5f) : new (7.5f,pos.Y,-12.5f)
                            : isCrystalOnNorthSide ? new (14,pos.Y,-15) : new (14,pos.Y,15);

            Vector3 startPoint_D2 = isCrystalOnEastSide
                            ? isCrystalOnNorthSide ? new (-19,pos.Y,-15) : new (-19,pos.Y,15)
                            : isCrystalOnNorthSide ? new (-7.5f,pos.Y,12.5f) : new (-7.5f,pos.Y,-12.5f);
            Vector3 endPoint_D2 = isCrystalOnEastSide
                            ? isCrystalOnNorthSide ? new (-14,pos.Y,-15) : new (-14,pos.Y,15)
                            : isCrystalOnNorthSide ? new (-7.5f,pos.Y,12.5f) : new (-7.5f,pos.Y,-12.5f);

            List<Vector3> startPoints = new List<Vector3>();
            startPoints.Add(startPoint_MT);
            startPoints.Add(startPoint_H1);
            startPoints.Add(startPoint_D1);
            startPoints.Add(startPoint_D2);

            List<Vector3> endPoints = new List<Vector3>();
            endPoints.Add(endPoint_MT);
            endPoints.Add(endPoint_H1);
            endPoints.Add(endPoint_D1);
            endPoints.Add(endPoint_D2);

            Vector3 myStartPoint = startPoints[accessory.GetMyIndex()];
            Vector3 myEndPoint = endPoints[accessory.GetMyIndex()];


            //绘图部分
            List<float[]> pointsList = new List<float[]>();
            pointsList.Add(new float[] {myStartPoint.X,myStartPoint.Y,myStartPoint.Z,5000,18000});
            pointsList.Add(new float[] {myEndPoint.X,myEndPoint.Y,myEndPoint.Z,0,3000});
            if(Math.Abs(myEndPoint.X)-7.5f < 0.2f){
                pointsList[0][4] = 14000;
                pointsList.RemoveAt(pointsList.Count - 1);
            }
            MultiDisDraw(pointsList,accessory);

            //绘制危险区
            accessory.FastDraw(DrawTypeEnum.Circle,crystalStay1,new(8),new(13000,10100),false);
            accessory.FastDraw(DrawTypeEnum.Circle,crystalStay2,new(8),new(13000,10100),false);
            accessory.FastDraw(DrawTypeEnum.Circle,crystalMove1,new(8),new(13000,10100),false);
            accessory.FastDraw(DrawTypeEnum.Circle,crystalMove2,new(8),new(13000,10100),false);

            accessory.Log.Debug($"Boss 1 Angry Seas Crystals Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 1 Angry Seas Crystals Guide : isCrystalOnEastSide => {isCrystalOnEastSide}");
            accessory.Log.Debug($"Boss 1 Angry Seas Crystals Guide : isCrystalOnNorthSide => {isCrystalOnNorthSide}");
            accessory.Log.Debug($"Boss 1 Angry Seas Crystals Guide : myStartPoint => {myStartPoint}");
            accessory.Log.Debug($"Boss 1 Angry Seas Crystals Guide : myEndPoint => {myEndPoint}");

            
        }


        
        
        
        #endregion

        #region Mob2

        [ScriptMethod(name: "小怪 2 止步龙卷 Mob 2 Stop Tornado", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35917|35795)$"])]
        public void Mob2_StopTornado(Event @event, ScriptAccessory accessory)
        {
            accessory.FastDraw(DrawTypeEnum.Circle,@event.GetTargetId(),new (4),new(0,4600),false);
        }

        [ScriptMethod(name: "小怪 2 拍手 Mob 2 Ovation", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35777|35796)$"])]
        public void Mob2_Ovation(Event @event, ScriptAccessory accessory)
        {
            accessory.FastDraw(DrawTypeEnum.Rect,@event.GetSourceId(),new (4,12),new(0,3900),false);
        }

        [ScriptMethod(name: "小怪 2 沉岛 Mob 2 Isle Drop", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35951|35900)$"])]
        public void Mob2_IsleDrop(Event @event, ScriptAccessory accessory)
        {
            accessory.FastDraw(DrawTypeEnum.Circle,@event.GetEffectPosition(),new (6),new(0,4900),false);
        }
        #endregion 

        #region Boss2
        // Boss2的场地中心坐标为 (200,-300,0) ,一个格子的边长是8

        /*
        正面未分析StatusID3726 StackCount151 Param663
        背面未分析StatusID3727 StackCount152 Param664
        右侧未分析StatusID3728 StackCount153 Param665
        左侧未分析StatusID3729 StackCount154 Param666
        

        Boos身上的3倍旋转角StatusID3938
        Boss身上的5倍旋转角StatusID3939

        在玩家身上3倍旋转角StatusID3721
        在玩家身上5倍旋转角StatusID3790

        光球DataId 16448
        黄箭头DataId 2013505
        白箭头DataId 2013506

        Get TargetIcon Id
        在玩家身上时
        顺时针旋转 01ED
        逆时针旋转 01EE
        X图标 01F8
        √图标 01F7

        在BOSS身上时
        顺时针旋转 01E4
        逆时针旋转 01E5
        
        */
        [ScriptMethod(name: "Boss 2 散火法 Boss 2 Inferno Theorem", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34990|35845)$"], userControl: false)]
        public void Boss2_InfernoTheorem(Event @event, ScriptAccessory accessory)
        {
            if(IsInSuppress(10000, nameof(Boss2_InfernoTheorem)))
            {
                return;
            }
            boss2_bossId = @event.GetSourceId();
            boss2_InfernoTheoremCasted =true;
            boss2_InfernoTheoremCastingCount++;
            accessory.Log.Debug($"Actived! => Boss 2 Id updated {boss2_bossId}");
        }


        //魔纹炮的安全检测需要同时获得boss读条+boss头上的目标类型
        //↑不必如此检测, boss的魔纹炮读条为34955-34958; 并且会有一个不可见的分身读条34959,该读条单位面向的背面则为安全区
        [ScriptMethod(name: "Boss 2 魔纹炮安全区 Boss 2 Arcane Blight Safe Zone", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34959|35814)$"])]
        public void Boss2_ArcaneBlightSafeZone(Event @event, ScriptAccessory accessory)
        {
            if(@event.GetSourceId() == boss2_bossId)
            {
                return;
            }

            //accessory.Log.Debug($"Actived! => Boss 2 Arcane Blight Safe Zone");
            float finalAngle = @event.GetSourceRotation() - MathF.PI;
            DrawPropertiesEdit dpFan = accessory.GetDrawPropertiesEdit(@event.GetSourceId(), new(20), 4500, true);
            dpFan.Radian = 0.5f * MathF.PI;
            dpFan.Rotation = - MathF.PI;
            dpFan.Color = accessory.Data.DefaultSafeColor.WithW(2.0f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpFan);

            //accessory.Log.Debug($"Boss 2 Arcane Blight Safe Zone : finalAngle => {finalAngle}");



        }

        [ScriptMethod(name: "Boss 2 双光球指路 Boss 2 double arcane globe guide", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3726|3727|3728|3729)$"])]
        public async void Boss2_doubleArcaneGlobeGuide(Event @event, ScriptAccessory accessory)
        {
            
            //7秒后场地黄白箭头和光球刷新
            if(@event.GetTargetId() != accessory.Data.Me || !boss2_InfernoTheoremCasted || await DelayMillisecond(7000))
            {
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 2 double arcane globe guide");
            boss2_InfernoTheoremCasted = false;
            /*
            要旋转多少角度才能把缺口对象目标(用于处理光球)
            正面未分析StatusID3726 => 0
            背面未分析StatusID3727 => PI
            右侧未分析StatusID3728 => -0.5PI
            左侧未分析StatusID3729 => 0.5PI

            玩家身上的未分析一共有三次引导，第一第二次是光球，第三次旋转角下的BOSS高精度光弹
            */
            List<float> baseAngles = new List<float>();
            baseAngles.Add(0);
            baseAngles.Add(MathF.PI);
            baseAngles.Add(-0.5f * MathF.PI);
            baseAngles.Add(0.5f * MathF.PI);
            float myBaseAngle = baseAngles[(int)@event.GetStatusID()-3726];


            Vector3 arcaneGlobePos1 = new (0,0,0);
            Vector3 arcaneGlobePos2 = new (0,0,0);


            //获得黄色箭头的实体

            uint yellowArrowDataId = 2013505;
            IEnumerable<IGameObject> yellowArrows = accessory.GetEntitiesByDataId(yellowArrowDataId);
            uint ballDataId = 16448;
            //零式难度为16606
            uint ballDataId2 = 16606;
            IEnumerable<IGameObject> _balls = accessory.GetEntitiesByDataId(ballDataId).Union(accessory.GetEntitiesByDataId(ballDataId2));
            List<IGameObject> balls = _balls.Where(obj => obj != null).Select(obj => (IGameObject)obj).ToList();

            foreach(IGameObject? obj in yellowArrows)
            {
                if(obj is IGameObject gameObject)
                {
                    Vector3 startPoint = gameObject.Position;
                    float rot = gameObject.Rotation;
                    Vector3 nextPoint = new (startPoint.X + MathF.Sin(rot) * 8f, startPoint.Y , startPoint.Z + MathF.Cos(rot) * 8f);
                    //如果能在nextPoint距离为2的范围内找到光球，那么就该光球则为第一个爆炸的光球
                    foreach(IGameObject ballObject in balls)
                    {
                        Vector3 ballPos = ballObject.Position;
                        if(Math.Sqrt(MathF.Pow(nextPoint.X - ballPos.X ,2) + MathF.Pow(nextPoint.Z - ballPos.Z ,2)) < 2)
                        {
                            //定位到第一个光球
                            arcaneGlobePos1 = ballPos;
                            List<IGameObject> anotherBall = balls.Where(obj => obj != ballObject).ToList();
                            if(anotherBall.Count > 0)
                            {
                                arcaneGlobePos2 = anotherBall[0].Position;
                            }
                            break;

                        }
                    }

                }
            }


            accessory.DrawTurnTowards(arcaneGlobePos1,new(20,0.33f*MathF.PI,myBaseAngle),new(9,5f),new(0,5500),accessory.Data.DefaultSafeColor,true);
            accessory.DrawTurnTowards(arcaneGlobePos2,new(20,0.33f*MathF.PI,myBaseAngle),new(9,5f),new(9500,5500),accessory.Data.DefaultSafeColor,true);
            
            accessory.Log.Debug($"Boss 2 double arcane globe guide: arcaneGlobePos1 => {arcaneGlobePos1}");
            accessory.Log.Debug($"Boss 2 double arcane globe guide: arcaneGlobePos2 => {arcaneGlobePos2}");
            accessory.Log.Debug($"Boss 2 double arcane globe guide: myBaseAngle => {myBaseAngle}");
        }

        [ScriptMethod(name: "Boss 2 高精度光弹指路 Boss 2 Targeted Light Guide", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(01ED|01EE)$"])]
        public void Boss2_TargetedLightGuide(Event @event, ScriptAccessory accessory)
        {
            

            //玩家会多次获得该ICON,注意区分次数
            if(@event.GetTargetId() != accessory.Data.Me)
            {
                return;
            }

            uint myStatusId = 0;
            uint myStatusOffset = 0;

            for (uint statusId = 3726; statusId <= 3729 ;statusId++)
            {
                bool isMeGet = accessory.isMeGetStatus(statusId);
                myStatusId = isMeGet ? statusId : myStatusId;
            }

            for (uint statusId = 3715; statusId <= 3718 ;statusId++)
            {
                bool isMeGet = accessory.isMeGetStatus(statusId);
                myStatusId = isMeGet ? statusId : myStatusId;
            }

            if(myStatusId == 0)
            {
                return;
            }

            
            accessory.Log.Debug($"Actived! => Boss 2 Targeted Light Guide");
            

            /*
                Get TargetIcon Id
                在玩家身上时
                顺时针旋转 01ED
                逆时针旋转 01EE

                在玩家身上3倍旋转角StatusID3721
                在玩家身上5倍旋转角StatusID3790

                正面未分析StatusID3726 StackCount151 Param663
                背面未分析StatusID3727 StackCount152 Param664
                右侧未分析StatusID3728 StackCount153 Param665
                左侧未分析StatusID3729 StackCount154 Param666

                移动命令前StatusID3715
                移动命令后StatusID3716
                移动命令左StatusID3717
                移动命令右StatusID3718

                      
                要旋转多少角度才能把缺口对象目标(用于处理光球)
                正面未分析StatusID3726 => 0
                背面未分析StatusID3727 => PI
                右侧未分析StatusID3728 => -0.5PI
                左侧未分析StatusID3729 => 0.5PI


                要你旋转多少角度才能把强制移动对向目标
                移动命令前StatusID3715 => 0
                移动命令后StatusID3716 => PI
                移动命令左StatusID3717 => 0.5PI
                移动命令右StatusID3718 => -0.5PI

        
            */



            object myTowardsObj = null;
            List<float> baseAngles = new List<float>();
            baseAngles.Add(0);
            baseAngles.Add(MathF.PI);

            if(myStatusId >= 3726)
            {
                //xx未分析机制
                myTowardsObj = boss2_bossId;
                myStatusOffset = 3726;
                baseAngles.Add(-0.5f * MathF.PI);
                baseAngles.Add(0.5f * MathF.PI);
            }else{
                //强制移动机制
                myTowardsObj = boss2_myTowardsPoint;
                myStatusOffset = 3715; 
                baseAngles.Add(0.5f * MathF.PI);
                baseAngles.Add(-0.5f * MathF.PI);
            }


            uint clockWiseIconId = 0x01ED;
            uint threeTimesStatusId = 3721;

            bool isMeGetClockwiseIcon = @event.GetIconId() == clockWiseIconId;

            //非3倍角
            bool isMeGetFive = !accessory.isMeGetStatus(threeTimesStatusId);
            float myBaseAngle = baseAngles[(int)(myStatusId - myStatusOffset)];
            


            //判断最终玩家的缺口会因为旋转角+顺逆时针，呈现出怎样的旋转方式
            //如果是顺时针旋转,则缺口角度将旋转 +0.5PI, 那么玩家的策略就是把缺口对象目标的角度 - 0.5PI
            //如果是逆时针旋转,则缺口角度将旋转 -0.5PI, 那么玩家的策略就是把缺口对象目标的角度 + 0.5PI

            float myModifyAngle = isMeGetFive 
                                    ? (isMeGetClockwiseIcon ? -0.5f * MathF.PI : 0.5f * MathF.PI)
                                    : (isMeGetClockwiseIcon ? 0.5f * MathF.PI : -0.5f * MathF.PI);
            float myFinalAngleTurnTowards = myBaseAngle + myModifyAngle;

            
            Vector2 delay_destoryAt = myStatusId >= 3726 ? new(7000,4500) : new (2000,9000);
            accessory.DrawTurnTowards(myTowardsObj,new(20,0.33f*MathF.PI,myFinalAngleTurnTowards),new(9,5f),delay_destoryAt,accessory.Data.DefaultSafeColor,true);

            accessory.Log.Debug($"Boss 2 Targeted Light Guide : myTowardsObj => {myTowardsObj}");
            accessory.Log.Debug($"Boss 2 Targeted Light Guide : isMeGetClockwiseIcon => {isMeGetClockwiseIcon}");
            accessory.Log.Debug($"Boss 2 Targeted Light Guide : isMeGetFive => {isMeGetFive}");
            accessory.Log.Debug($"Boss 2 Targeted Light Guide : myModifyAngle => {myModifyAngle}");
            accessory.Log.Debug($"Boss 2 Targeted Light Guide : myBaseAngle => {myBaseAngle}");
            accessory.Log.Debug($"Boss 2 Targeted Light Guide : myFinalAngleTurnTowards => {myFinalAngleTurnTowards}");
            

            
        }

        /*
        如何计算扫雷的引导位置和面向角度
        检测读条34970, EffectPosition 的位置 移动 targetRotation x 4 获得AOE的中心位置
        离场地中心最远的三个点，由点向中心做向量，三个向量之和，即为场地中心到缺口的向量值，根据该向量获得旋转角度，旋转引导图样
        面向角度计算类似高精度光弹指路
        减算爆雷a StatusID 3724
        减算爆雷b StatusID 3725
        */

        [ScriptMethod(name: "Boss 2 地雷魔纹指路 Boss 2 Arcane Mine Guide", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34970|35825)$"])]
        public void Boss2_ArcaneMineGuide(Event @event, ScriptAccessory accessory)
        {
            lock(_lock)
            {
                Vector3 minePos = @event.GetEffectPosition();
                float rot = @event.GetSourceRotation() + 0.5f * MathF.PI; //<= 狒狒y轴正方向为0
                Vector3 mineCentrePos = new (minePos.X + 4 * MathF.Cos(rot),minePos.Y, minePos.Z + 4 * MathF.Sin(rot));
                boss2_ArcaneMinesList.Add(mineCentrePos);
                if(boss2_ArcaneMinesList.Count < 8)
                {
                    return;
                }
            }
            
            //如果进入到这，通常说明收集到了8个地雷的信息
            accessory.Log.Debug($"Actived! => Boss 2 Arcane Mine Guide");
            Vector3 oPoint = new (200,-300,0);
            List<Vector3> mines = boss2_ArcaneMinesList.OrderByDescending(v3 => Util.DistanceBetweenTwoPoints(v3,oPoint)).ToList();
            Vector3 oToCorner = new (3 * oPoint.X - (mines[0].X + mines[1].X + mines[2].X),
                                     3 * oPoint.Y - (mines[0].Y + mines[1].Y + mines[2].Y),
                                     3 * oPoint.Z - (mines[0].Z + mines[1].Z + mines[2].Z));

            float cornerRot = MathF.Atan2(oToCorner.Z,oToCorner.X);
            float myModifyAngle = cornerRot - 0.25f * MathF.PI;
            //以缺角在场地右下角为图样 通常此时的cornerRot为0.25PI
            Vector3 debuffStack1_template = new (216,-300,5.5f);
            Vector3 debuffStack3_template = new (192,-300,12.5f);
            Vector3 debuffStack2_template1 = new (216,-300,1.5f);
            Vector3 debuffStack2_template2 = new (216,-300,-9.5f);

            /*
                如果, 2+3分摊, 2分摊的纵坐标必须为0
                如果, 2+2分摊, 随意
                如果, 2+1分摊, 2分摊的纵坐标必须为-8
                如果, 1+3分摊, 随意
            */
            uint stackDebuffId = 3724;
            uint surgeVectorDebuffId = 3723 ;
            Status? myDebuffInfo = accessory.GetStatusInfo(accessory.Data.Me,stackDebuffId);
            int myDebuffStack = myDebuffInfo == null ? 0 : ((Status)myDebuffInfo).StackCount;
            Vector3 myStartPoint = new (200,-300,0);
            Vector3 myTemplate = new (200,-300,0);
            float myTowardsRot = 0;
            bool isMeGetsurgeVectorDebuff = accessory.isMeGetStatus(surgeVectorDebuffId);
            switch(myDebuffStack)
            {
                case 1:
                    myTowardsRot =  0.5f * MathF.PI + 0.5f * MathF.PI;
                    myTemplate = debuffStack1_template;
                    break;
                case 3:
                    myTowardsRot =  1 * MathF.PI + 0.5f * MathF.PI;
                    myTemplate = debuffStack3_template;
                    break;
                case 2:
                    myTowardsRot =  0.5f * MathF.PI + 0.5f * MathF.PI;
                    List<uint> surgeVectorPlayers = accessory.whoGetStatusInParty(surgeVectorDebuffId);
                    

                    if(surgeVectorPlayers.Count == 2)
                    {   
                        int _count1 = accessory.GetStatusInfo(surgeVectorPlayers[0],stackDebuffId)?.Param ?? 100;
                        int _count2 = accessory.GetStatusInfo(surgeVectorPlayers[1],stackDebuffId)?.Param ?? 100;
                        int surgeVectorPlayersStackCount = _count1 + _count2;
                        // int surgeVectorPlayersStackCount = ((accessory.GetStatusInfo(surgeVectorPlayers[0],stackDebuffId)?.Param ?? 100) + 
                        //    (accessory.GetStatusInfo(surgeVectorPlayers[1],stackDebuffId)?.Param ?? 100));
                        //     以下是一个代码书写错误，下侧代码内容第一行 ?.Param ?? 后侧的 100 + accessory.GetStatusInfo(surgeVectorPlayers[1],stackDebuffId)?.Param ?? 100) 
                        //     被看作了一个整体,只会返回2
                        //     surgeVectorPlayersStackCount = (accessory.GetStatusInfo(surgeVectorPlayers[0],stackDebuffId)?.Param ?? 100 + 
                        //    accessory.GetStatusInfo(surgeVectorPlayers[1],stackDebuffId)?.Param ?? 100);
                        if( surgeVectorPlayersStackCount == 5)
                        {
                            //如果是2+3分摊
                           
                            myTemplate = isMeGetsurgeVectorDebuff?debuffStack2_template1:debuffStack2_template2;
                        }
                        else if(surgeVectorPlayersStackCount == 3)
                        {
                            //如果是1+2分摊
                            myTemplate = isMeGetsurgeVectorDebuff?debuffStack2_template2:debuffStack2_template1;
                        }
                        else{
                            //根据和外一个2层buff
                            uint another2stackPlayer = 0;
                            List<uint> others = accessory.Data.PartyList.Except(new List<uint> { accessory.Data.Me }).ToList();
                            foreach (uint playerId in others)
                            {
                                Status? debuffInfo = accessory.GetStatusInfo(playerId,stackDebuffId);
                                bool isStack2 = (debuffInfo?.StackCount ?? 100) == 2;
                                if(isStack2)
                                {
                                    another2stackPlayer = playerId;
                                    break;
                                }

                            }
                            bool isMePriority = accessory.GetMyIndex() < accessory.GetIndexInParty(another2stackPlayer);
                            myTemplate = isMePriority? debuffStack2_template2 : debuffStack2_template1;
                        }
                         accessory.Log.Debug($"Boss 2 Arcane Mine Guide : surgeVectorPlayersStackCount => {surgeVectorPlayersStackCount}");
                    }
                    break;
                case 0:
                default:break;
            }

            Vector3 myTowardsPoint_template = new (myTemplate.X + MathF.Cos(myTowardsRot) * 80, myTemplate.Y, myTemplate.Z + MathF.Sin(myTowardsRot) * 80);
            myStartPoint = Util.RotatePointInFFXIVCoordinate(myTemplate,oPoint,myModifyAngle);
            boss2_myTowardsPoint = Util.RotatePointInFFXIVCoordinate(myTowardsPoint_template,oPoint,myModifyAngle);

            //绘图部分

            MultiDisDraw(new List<float[]>{new float[]{myStartPoint.X,myStartPoint.Y,myStartPoint.Z,0,10000}},accessory);



            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : cornerRot => {cornerRot}");
            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : myDebuffStack => {myDebuffStack}");
            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : isMeGetsurgeVectorDebuff => {isMeGetsurgeVectorDebuff}");
            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : myStartPoint => {myStartPoint}");
            accessory.Log.Debug($"Boss 2 Arcane Mine Guide : myTowardsPoint => {boss2_myTowardsPoint}");
        }



        [ScriptMethod(name: "Boss 2 三树人指路 Boss 2 Golems Guide", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16449|16607)$"])]
        public async void Boss2_GolemsGuide(Event @event, ScriptAccessory accessory)
        {
            if(IsInSuppress(10000, nameof(Boss2_GolemsGuide)) || await DelayMillisecond(7000))
            {
                return;
            }
            uint yellowArrowDataId = 2013505;
            List<Vector3> cornerYellowArrow = accessory.GetEntitiesByDataId(yellowArrowDataId).Where(obj => obj != null).Select(obj => obj.Position)
                        .Where(v3 => Math.Round(Math.Abs(v3.X - 200) - Math.Abs(v3.Z - 0)) == 0 ).ToList();
                
            if((cornerYellowArrow?.Count ?? 0) < 1)
            {
                return;
            }
            uint golemDataId = @event.GetDataId();
            List<Vector3> golemsPos = accessory.GetEntitiesByDataId(golemDataId).Where(obj => obj != null).Select(obj => obj.Position).ToList();
            List<uint> golemIds = accessory.GetEntitiesByDataId(golemDataId).Where(obj => obj != null).Select(obj => obj.EntityId).ToList();
            //判断新的北侧是哪, 以及是2+4树人还是3+5树人
            //如果有两个树人的距离大于6且小于10 则为 2+4 树人
            bool is24typeGoloms = false;
            foreach (Vector3 pos in golemsPos)
            {
                foreach (Vector3 posCompareTo in golemsPos)
                {
                    float distance = Util.DistanceBetweenTwoPoints(pos,posCompareTo);
                    is24typeGoloms = is24typeGoloms || (distance > 6 && distance <10);
                }
                if(is24typeGoloms)
                {
                    break;
                }
            }


            List<Vector3> template24_MT = new List<Vector3> 
            {
                new (-16 + 200,-300,0), new (-11.5f + 200,-300,-3.5f), new (-4.5f + 200,-300,4)
            };
            List<Vector3> template35_MT = new List<Vector3> 
            {
                new (-11.5f + 200,-300,0), new (-19.5f + 200,-300,-3.5f), new (-12.5f + 200,-300,4)
            };

            List<Vector3> template24_D1 = new List<Vector3> 
            {
                new (-12.5f + 200,-300,4.5f), new (-11.5f + 200,-300,11.5f), new (-4.5f + 200,-300,4)
            };
            List<Vector3> template35_D1 = new List<Vector3> 
            {
                new (-11.5f + 200,-300,4.5f), new (-19.5f + 200,-300,11.5f), new (-12.5f + 200,-300,4)
            };

            List<Vector3> template24_H1 = new List<Vector3> 
            {
                new (3.5f + 200,-300,-3.5f), new (11.5f + 200,-300,-3.5f), new (4.5f + 200,-300,4)
            };
            List<Vector3> template35_H1 = new List<Vector3> 
            {
                new (4.5f + 200,-300,-3.5f), new (3.5f + 200,-300,-3.5f), new (-3.5f + 200,-300,4)
            };

            List<Vector3> template24_D2 = new List<Vector3> 
            {
                new (3.5f + 200,-300,11.5f), new (11.5f + 200,-300,11.5f), new (4.5f + 200,-300,4)
            };
            List<Vector3> template35_D2 = new List<Vector3> 
            {
                new (4.5f + 200,-300,11.5f), new (3.5f + 200,-300,11.5f), new (-3.5f + 200,-300,4)
            };

            uint surgeVectorDebuffId = 3723 ;
            List<uint> debuffPlayers = accessory.whoGetStatusInParty(surgeVectorDebuffId);
            bool isSurgeDebuffSameGroup = debuffPlayers.Count == 2 && Math.Abs(accessory.GetIndexInParty(debuffPlayers[0]) - accessory.GetIndexInParty(debuffPlayers[1])) == 2;
            
            List<Vector3>[] pointsTable = new List<Vector3>[]
            {
                is24typeGoloms ? template24_MT : template35_MT,
                is24typeGoloms ? template24_H1 : template35_H1,
                is24typeGoloms ? (!isSurgeDebuffSameGroup?template24_D1:template24_D2):(!isSurgeDebuffSameGroup?template35_D1:template35_D2),
                is24typeGoloms ? (!isSurgeDebuffSameGroup?template24_D2:template24_D1):(!isSurgeDebuffSameGroup?template35_D2:template35_D1),
            };
            List<Vector3> _myPointsListTemplate = pointsTable[accessory.GetMyIndex()];
            Vector3 orginPoint = new (200,-300,0);
            float rad = MathF.Atan2(cornerYellowArrow[0].Z,cornerYellowArrow[0].X - 200) - 0.25f * MathF.PI;
            List<Vector3> _myPointsList = new List<Vector3>{
                Util.RotatePointInFFXIVCoordinate(_myPointsListTemplate[0],orginPoint,rad),
                Util.RotatePointInFFXIVCoordinate(_myPointsListTemplate[1],orginPoint,rad),
                Util.RotatePointInFFXIVCoordinate(_myPointsListTemplate[2],orginPoint,rad),
            };
            List<float[]> myPointsList = new List<float[]> 
            {
                new float[] {_myPointsList[0].X,_myPointsList[0].Y,_myPointsList[0].Z,0,12000},
                new float[] {_myPointsList[1].X,_myPointsList[1].Y,_myPointsList[1].Z,0,9000},
                new float[] {_myPointsList[2].X,_myPointsList[2].Y,_myPointsList[2].Z,0,4000},
            };
            MultiDisDraw(myPointsList,accessory);


            //画危险区
            foreach (uint id in golemIds)
            {
                accessory.FastDraw(DrawTypeEnum.Rect,id,new(8,40),new(0,15000 - 3800),false);
            }

            List<Vector3> dangerousZonePoints_template = new List<Vector3>
            {
                new (20+200,-300,16),
                new (20+200,-300,-8)
            };
            List<Vector3> dangerousZonePoints = new List<Vector3>
            {
                Util.RotatePointInFFXIVCoordinate(dangerousZonePoints_template[0],orginPoint,rad),
                Util.RotatePointInFFXIVCoordinate(dangerousZonePoints_template[1],orginPoint,rad),
            };
            DrawPropertiesEdit dpRect = accessory.GetDrawPropertiesEdit(dangerousZonePoints[0],new(8,42),15000 - 3800,false);
            dpRect.Rotation = - 0.5f * MathF.PI - rad;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRect);
            dpRect.Position = dangerousZonePoints[1];
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRect);

            accessory.Log.Debug($"Actived! => Boss 2 Golems Guide");
            accessory.Log.Debug($"Boss 2 Golems Guide : cornerYellowArrow => {cornerYellowArrow[0]}");
            accessory.Log.Debug($"Boss 2 Golems Guide : is24typeGoloms => {is24typeGoloms}");
            accessory.Log.Debug($"Boss 2 Golems Guide : isSurgeDebuffSameGroup => {isSurgeDebuffSameGroup}");
            accessory.Log.Debug($"Boss 2 Golems Guide : myPointsList0 => {_myPointsList[0]}");
            accessory.Log.Debug($"Boss 2 Golems Guide : myPointsList1 => {_myPointsList[1]}");
            accessory.Log.Debug($"Boss 2 Golems Guide : myPointsList2 => {_myPointsList[2]}");


        }

        

        //立体爆雷指路
        [ScriptMethod(name: "Boss 2 立体爆雷战术 Boss 2 Spatial Tactics", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34976|35831)$"])]
        public async void Boss2_SpatialTactics(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"Actived! => Boss 2 Spatial Tactics");
            if(await DelayMillisecond(12000))
            {
                return;
            }

            uint ballDataId = 16448;
            //零式难度为16606
            uint ballDataId2 = 16606;
            IEnumerable<IGameObject> _balls = accessory.GetEntitiesByDataId(ballDataId).Union(accessory.GetEntitiesByDataId(ballDataId2));
            List<IGameObject> balls = _balls.Where(obj => obj != null).Select(obj => (IGameObject)obj).ToList();
            if(balls.Count < 1)
            {
                return;
            }
            Vector3 originPos = new (200,-300,0);
            Vector3 ballPos = balls[0].Position;
            double _newLeftUp = Math.Round((-0.32f + MathF.Atan2(ballPos.Z - originPos.Z,ballPos.X - originPos.X))/(0.25f * MathF.PI));
            float newLeftUp = (float)_newLeftUp * 0.25f * MathF.PI;
            //以球在场地东北角为模板 此时的newLeftUp应该为 -0.25 PI
            Vector3 behindPoint1_template = new (200,-300,12);
            Vector3 behindPoint2_template = new (200,-300,4);
            Vector3 behindPoint1 = Util.RotatePointInFFXIVCoordinate(behindPoint1_template,originPos,newLeftUp + 0.25f * MathF.PI);
            Vector3 behindPoint2 = Util.RotatePointInFFXIVCoordinate(behindPoint2_template,originPos,newLeftUp + 0.25f * MathF.PI);

            DrawPropertiesEdit dpRect = accessory.GetDrawPropertiesEdit(behindPoint1, new (8,8), 6000, true);
            dpRect.TargetPosition = originPos;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRect);
            dpRect.Position = behindPoint2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRect);
        }
        

        #endregion


        #region Boss3



        /*
        Boss3场地中心为(-200,-200,0)
        14.17.51.458 第一次花式装填 +14s 装填完成
        14.18.06.936 +15s 第一次陷阱射击
        14.18.16.309 +25s 开心扳机
        14.18.29.502 +39s 炸弹出现
        14.18.38.336 +47s 第二次陷阱射击
        第一次花式装填，不做指路内容，仅提示AOE范围

        14.18.55.311 第二次花式装填
                     +25s 出现飞镖buff
        14.19.24.769 +29s 炸弹转完了
        14.19.27.636 +32s 第一次陷阱射击 (分摊分散同时判定飞镖)
        14.19.59.781 +64s 开心扳机
        14.20.07.736 +72s 第二次陷阱射击读条
        14.20.13.902 +78s 强制移动判定
        第二次花式装填，暂不做指路内容
        提示炸弹AOE范围
        第一次陷阱射击，如果是分摊,用脚下较小的AOE圈提示去什么颜色的地块
        第一次陷阱射击，如果是分散,则标记分散AOE圈的同时，用给圈着色的方式，引导去什么颜色的地块


        运动会,计算正北
        提示炸弹AOE范围
        找到爆炸炸弹，计算最近的数字点为正北角度，仅做大致方向指引

        第二次转盘，计算正北
        飞镖总是落在内侧,根据下侧表格，可以计算出正北角度
        根据火的顺逆方向，计算分摊组的走位
        如果飞镖点了同一组(MT+H1)或者(D1+D2),则分摊组两人的起点需要对换，终点不变


        14.22.01.118 第三次花式装填
        14.22.15.569 +14.4s 第一次陷阱射击
        14.22.28.836 +27.7s 礼物箱完成召唤,同时需要为开心扳机预指路
        14.22.36.704 +35.5s 开心扳机,同时+2s后短buff判定
        14.22.43.669 +42.5s 第二次陷阱射击读条，+5s后长buff判定，长buff需要处理分摊分散+炸弹AOE
        */





        [ScriptMethod(name: "Boss 3 花式装填初始化 Boss 3 Trick Reload Init", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35146|35175)$"], userControl: false)]
        public void Boss3_TrickReloadInit(Event @event, ScriptAccessory accessory)
        { 
            boss3_trickReloads.Clear();
            boss3_trickReloadsCount ++ ;
            accessory.Log.Debug($"Actived! => Boss 3 Trick Reload Init {boss3_trickReloadsCount}");
        
        }


        //记录装填失败35110+装填成功35109
        [ScriptMethod(name: "Boss 3 花式装填记录 Boss 3 Trick Reload Log", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(35110|35109)$"])]
        public async void Boss3_TrickReloadLog(Event @event, ScriptAccessory accessory)
        {

            //提示分散,第三次提示开心扳机安全区
            boss3_trickReloads.Add(@event.GetActionId());
            if(boss3_trickReloads.Count!= 8)
            {
                return;
            }
            //是先分散吗
            uint loadedActionId = 35109;
            uint misloadedActionId = 35110;
            bool isDisperseFirst = boss3_trickReloads[0] == loadedActionId;
            int safeNumber = boss3_trickReloads.GetRange(1,6).IndexOf(misloadedActionId);
            Vector2 firstShotDelay_destoryAt = new (0,8000);
            Vector2 secondShotDelay_destoryAt = new(30000,8000);
            switch(boss3_trickReloadsCount)
            {
                case 1:
                    firstShotDelay_destoryAt = new (0,9000);
                    secondShotDelay_destoryAt = new(27000,15000-100);
                    break;
                case 2:
                    firstShotDelay_destoryAt = new (15000,15000-2800);
                    secondShotDelay_destoryAt = new(55000,12500-100);
                    break;
                case 3:
                    firstShotDelay_destoryAt = new (0,9000);
                    secondShotDelay_destoryAt = new(24200,12500);
                    break;
                case 0:
                default:
                    break;
            }


            //分摊分散绘图 , 暂时只绘制分散
            DrawPropertiesEdit dpCircle = accessory.GetDrawPropertiesEdit(accessory.Data.Me, new(6.1f),8000,false);
            dpCircle.Delay = (long)(isDisperseFirst ? firstShotDelay_destoryAt.X : secondShotDelay_destoryAt.X);
            dpCircle.DestoryAt = (long)(isDisperseFirst ? firstShotDelay_destoryAt.Y : secondShotDelay_destoryAt.Y);
            dpCircle.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
            foreach (uint id in accessory.Data.PartyList)
            {
                dpCircle.Owner = id;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
            }


            //DrawPropertiesEdit dpDount = accessory.GetDrawPropertiesEdit(accessory.Data.Me, new(6.1f),0,false);


        
        
        

            if(boss3_trickReloadsCount == 2){
                //等待飞镖buff出现
                if(await DelayMillisecond(11000)){
                    return;
                }
                //MT H1 D1
                List<Vector4> colors = new List<Vector4>
                {
                    // MT,H1,D1
                    new (0,0,1,10),
                    new (0.83f,0.855f,0.165f,10),
                    new (1,0,0,10),
                };
                //检测飞镖StatusID 3742
                Vector4 myColor = new (1,1,1,10);
                uint eyeStatusId = 3742 ;
                bool isMeGetEyeStatus = accessory.isMeGetStatus(eyeStatusId);
                if(accessory.GetMyIndex()<=2)
                {
                    myColor = isMeGetEyeStatus ? colors[accessory.GetMyIndex()] : myColor ;
                }
                else
                {
                    //D2
                    if(isMeGetEyeStatus)
                    {
                        //D2,有debuff
                        //如果MT,H1有人没debuff,就去那个人的颜色
                        List<uint> others = accessory.whoGetStatusInPartyWithoutMe(eyeStatusId);
                        if(others.Count == 2)
                        {
                            switch(accessory.GetIndexInParty(others[0]) + accessory.GetIndexInParty(others[1]))
                            {
                                case 0 + 1 :
                                    myColor = colors[2];
                                    break;
                                case 0 + 2 : 
                                    myColor = colors[1];
                                    break;
                                case 1 + 2 : 
                                    myColor = colors[0];
                                    break;
                            }
                        }
                                                                                                                           
                    }

                }
                DrawPropertiesEdit dpSmallCircle = accessory.GetDrawPropertiesEdit(accessory.Data.Me, new(1.1f),16000,false);
                dpSmallCircle.Color = myColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpSmallCircle);
                accessory.Log.Debug($"Boss 3 Trick Reload Log : my order number in party => {1 + accessory.GetMyIndex()}");
                accessory.Log.Debug($"Boss 3 Trick Reload Log : myColor => {myColor}");

            }

            if(boss3_trickReloadsCount == 3)
            {
                //画一个麻将安全区引导,延迟12秒，显示10秒
                DrawPropertiesEdit dpFan = accessory.GetDrawPropertiesEdit(new Vector3(-200,-200,0), new(20.1f),10000,true);
                dpFan.Radian = MathF.PI / 3f ;
                dpFan.Color = accessory.Data.DefaultSafeColor.WithW(0.4f);
                dpFan.Delay = 12000;
                //起始坐标为狒坐标 -0.5π,顺逆时相反
                float _rot = -1f * MathF.PI + safeNumber * MathF.PI/3f ;
                dpFan.Rotation = -_rot;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpFan);
            }

        }


        // 该部分代码参考了 Cyf5119 的 AalBomb 脚本
        [ScriptMethod(name: "Boss 3 炸弹 Boss 3 Bomb ", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16481|16489)$"])]
        public async void Boss3_Bomb(Event @event, ScriptAccessory accessory)
        {

            if(IsInSuppress(5000, nameof(Boss3_Bomb)))
            {
                return;
            }
            boss3_bombsRound++;
            uint bombDataId = @event.GetDataId();

            /*
            第一次炸弹出现到爆炸 16s
            第二次炸弹出现到爆炸 19.3s
            第三次炸弹出现到爆炸 18.1s
            第四次炸弹出现到爆炸 16.5s
            */

            int bombDelayTime = 0;
            switch(boss3_bombsRound)
            {
                case 1 :
                    bombDelayTime = 16000;
                    break;
                case 2 :
                    bombDelayTime = 19300;
                    break;
                case 3 :
                    bombDelayTime = 18100;
                    break;
                case 4 :
                    bombDelayTime = 16500;
                    break;
                default :
                    bombDelayTime = 19500;
                    break;
            }

            //时长为五秒钟的0号炸弹寻找
            for(int times = 0 ; times < 50 ; times ++)
            {
                IBattleChara _bombsDetect0 = null;
                unsafe
                {
                    List<IBattleChara> _bombsDetect = accessory.GetEntitiesByDataId(bombDataId).Where(obj => obj as IBattleChara != null).Cast<IBattleChara>().ToList();
                    foreach (IBattleChara _bomb in _bombsDetect)
                    {
                        FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* _bombStructPtr = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)_bomb.Address;
                        if(!object.Equals((*_bombStructPtr), null))
                        {
                            //有发光特效的的炸弹的.Timeline.AnimationState[0]值是1,不发光的是0
                            if((*_bombStructPtr).Timeline.AnimationState[0] > 0)
                            {
                                _bombsDetect0 = _bomb;
                            }
                        }
                    }

                }
                if(_bombsDetect0 != null)
                {
                    bombDelayTime -= times * 100;
                    accessory.Log.Debug($"Boss 3 Bomb : detect times => {times}");
                    break;
                }
                if(await DelayMillisecond(100))
                {
                    break;
                }

            }

            
            if(await DelayMillisecond(300))
            {
                bombDelayTime -= 300;
                return;
            }

            accessory.Log.Debug($"Actived! => Boss 3 Bomb {boss3_bombsRound}");
            
            List<IBattleChara> _bombsList = accessory.GetEntitiesByDataId(bombDataId).Where(obj => obj as IBattleChara != null).Cast<IBattleChara>().ToList();
            IBattleChara bomb0 = null;

            //存放会爆炸的炸弹列表
            List<IBattleChara> bombsList = new List<IBattleChara>();
            unsafe
            {
                foreach (IBattleChara _bomb in _bombsList)
                {
                    FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* _bombStructPtr = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)_bomb.Address;
                    if(!object.Equals((*_bombStructPtr), null))
                    {
                        //有发光特效的的炸弹的.Timeline.AnimationState[0]值是1,不发光的是0
                        if((*_bombStructPtr).Timeline.AnimationState[0] > 0)
                        {
                            bomb0 = _bomb;
                        }
                    }
                }


                if(bomb0 != null)
                {
                    bombsList.Add(bomb0);
                    accessory.Log.Debug($"Boss 3 Bomb : bomb0 Id => {bomb0.EntityId}");
                    FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* bomb0StructPtr = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)bomb0.Address;
                    /*
                        (*bomb0StructPtr).Vfx.Tethers[0].TargetId.ObjectId 的值为连线目标对象
                        (*bomb0StructPtr).Vfx.Tethers[1].TargetId.ObjectId 的值固定为0xE0000000,似乎是用于标记结尾
                    */

                    //通常(*bomb0StructPtr).Vfx.Tethers[0].TargetId.ObjectId 为第二个炸弹，通过该Id查找第三个炸弹
                    uint _bomb1Id = (*bomb0StructPtr).Vfx.Tethers[0].TargetId.ObjectId;
                    List<IBattleChara> _bomb1 = _bombsList.Where(obj => obj.EntityId == _bomb1Id).ToList();
                    if(_bomb1.Count > 0)
                    {
                        //找到了第二个炸弹
                        bombsList.Add(_bomb1[0]);
                        FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* bomb1StructPtr = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)_bomb1[0].Address;
                        uint _bomb2Id = (*bomb1StructPtr).Vfx.Tethers[0].TargetId.ObjectId;

                        List<IBattleChara> _bomb2 = _bombsList.Where(obj => obj.EntityId == _bomb2Id).ToList();
                        if(_bomb2.Count >0)
                        {
                            bombsList.Add(_bomb2[0]);
                        }
                        
                    }
                }
            }

            if(bombsList.Count != 3)
            {
                return;
            }

            List<IBattleChara> dudsList = _bombsList.Except(bombsList).ToList();

            unsafe
            {
                foreach (IBattleChara dud in dudsList)
                {
                    FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* dudStructPtr = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)dud.Address;
                    //dudStructPtr->DrawObject->IsVisible = false;
                    dudStructPtr->SetDrawOffset(0,-5,0);
                }
            }


            
            /*
                传递一下哪个炸弹离哪个数字点最近
                点1, -0.25π,19
                点2, 0.25π,19
                点3, 0.75π,19
                点4, -0.75π,19
                计算每个点到三个炸弹的距离，取其中最短的距离作为该点的 标识距离
                按照标识距离将每个点排序
            */
            Vector3 originPos = new (-200,-200,0);
            List<float[]> rad_distance = new List<float[]>
            {
                new float[]{-0.25f * MathF.PI,100},
                new float[]{0.25f * MathF.PI,100},
                new float[]{0.75f * MathF.PI,100},
                new float[]{-0.75f * MathF.PI,100}
            };

            for(int i = 0 ; i < rad_distance.Count ; i++ )
            {
                float[] r_d = rad_distance[i] ;
                foreach (IBattleChara _bomb in bombsList)
                {
                    Vector3 _bombPos = _bomb.Position;
                    Vector3 pointPos = new (MathF.Cos(r_d[0]) * 19f + originPos.X ,_bombPos.Y,MathF.Sin(r_d[0]) * 19f + originPos.Z);
                    float _distance = Util.DistanceBetweenTwoPoints(_bombPos,pointPos);
                    rad_distance[i][1] = _distance < rad_distance[i][1] ? _distance : rad_distance[i][1];
                }
            }
            //传递参数
            boss3_rad_distance = rad_distance.OrderBy(r_d => r_d[1]).ToList();


            Vector4 _color = boss3_bombsRound == 2 ? accessory.Data.DefaultDangerColor.WithW(4) : accessory.Data.DefaultDangerColor ;
            //绘图部分
            foreach (IBattleChara bomb in bombsList)
            {
                
                accessory.FastDraw(DrawTypeEnum.Circle,bomb.EntityId,new (12),new (bombDelayTime - 7000 ,7000),_color);
            }

        }

        /*
        场地的飞镖地形只有一种类型, 以狒坐标为基准X轴正方向为0PI

        *PI           内      外
        0/12 + 1/24   蓝      绿
        1/12 + 1/24   红      蓝
        2/12 + 1/24   绿      红

        循环填充至于11/12
        */
        [ScriptMethod(name: "Boss 3 喷火 Boss 3 Fire Spread", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35154|35183)$"], userControl: false)]
        public void Boss3_FireSpread(Event @event, ScriptAccessory accessory)
        { 
            lock(_lock)
            {
                boss3_fireSpreadRotation.Add(0.5f * MathF.PI - @event.GetSourceRotation());
                boss3_burningChainsPlayers.Clear();
                //accessory.Log.Debug($"Actived! => Boss 3 Fire Spread {boss3_fireSpreadRotation.Count}");
            }
            
        }
        [ScriptMethod(name: "Boss 3 火焰链 Boss 3 Burning Chains", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0061)$"], userControl: false)]
        public void Boss3_BurningChains(Event @event, ScriptAccessory accessory)
        {
            lock(_lock)
            {
                boss3_burningChainsPlayers.Add(@event.GetTargetId());
                //accessory.Log.Debug($"Actived! => Boss 3 Burning Chains {boss3_burningChainsPlayers.Count}");
            }
        }

        [ScriptMethod(name: "Boss 3 火球顺逆 Boss 3 Fire Ball Clockwise", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(009C|009D)$"], userControl: false)]
        public void Boss3_FireBallClockwise(Event @event, ScriptAccessory accessory)
        {
            boss3_isFireBallClockwise = @event.GetIconId() == 0x009C;
            //accessory.Log.Debug($"Actived! => Boss 3 Fire Ball Clockwise {boss3_isFireBallClockwise}");
        }




        [ScriptMethod(name: "Boss 3 第二次飞镖盘 Boss 3 Second Dart Board", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16478|16486)$"])]
        public async void Boss3_SecondDartBoard(Event @event, ScriptAccessory accessory)
        {
            if( await DelayMillisecond(2500) || boss3_fireSpreadRotation.Count < 3 )
            {
                //等待boss喷火读条信息收集完成
                return;
            }
            accessory.Log.Debug($"Actived! => Boss 3 Second Dart Board");
            //火球顺逆时针
            bool isFireBallClockwise = boss3_isFireBallClockwise;
            //查找诱导魔纹在哪
            Vector3 _homingPatternPos = @event.GetSourcePosition();
            Vector3 originPos = new (-200,-200,0);
            float _homingRot = MathF.Atan2(_homingPatternPos.Z - originPos.Z,_homingPatternPos.X - originPos.X);
            int _homingNum = (int)Math.Round((_homingRot + 2f * MathF.PI - MathF.PI/12f) / (MathF.PI/6f)) % 3;
            //如果是0,则落点为蓝色,新12点为角度 0 + (π/2)x整数 ;1,红色,π/6 + (π/2)x整数 ;2,绿色,π/3 + (π/2)x整数;
            float newNorthRot = -0.5f * MathF.PI;
            foreach (float fireSpreadRot in boss3_fireSpreadRotation)
            {
                newNorthRot = _homingNum == ((int)Math.Round((fireSpreadRot + 2f * MathF.PI)/(MathF.PI/6f)) % 3) ? fireSpreadRot : newNorthRot;
            }
            //获得了新的正北角度
            //在新北画一个圆圈用作提示
            Vector3 circlPos = new (MathF.Cos(newNorthRot) * 8f + originPos.X,originPos.Y,MathF.Sin(newNorthRot) * 8f + originPos.Z);
            accessory.FastDraw(DrawTypeEnum.Circle,circlPos,new (2.5f),new (0,10000),accessory.Data.DefaultSafeColor.WithW(8));

            //等待锁链buff出现
            if( await DelayMillisecond(9000-2500) || boss3_burningChainsPlayers.Count < 2 )
            {
                //等待火焰链标记收集完成
                return;
            }
            bool isMeGetBurningChains = boss3_burningChainsPlayers.Contains(accessory.Data.Me);
            //以B点为新的正北点为模板
            Vector3 burningChainsLeft_template = new (-200-0.5f,-200,-18.5f);
            Vector3 burningChainsRight_template = new (-200-0.5f,-200,18.5f);
            Vector3 noBurningChainsLeft_template = new (-200 + 18.5f,-200,-0.5f);
            Vector3 noBurningChainsRight_template = new (-200 + 18.5f,-200,0.5f);
            int[] roleMarkPriority = new int[] { (int)RoleMarkEnum.MT, (int)RoleMarkEnum.H1, (int)RoleMarkEnum.D1, (int)RoleMarkEnum.D2};
            Vector3 myStartPoint = new (-200,-200,0);
            Vector3 myEndPoint = new (-200,-200,0);
            bool isMeGoLeft = false;
            if(isMeGetBurningChains)
            {
                List<uint> anotherOneId = boss3_burningChainsPlayers.Except(new List<uint>{accessory.Data.Me}).ToList();
                isMeGoLeft = anotherOneId.Count > 0 && Array.IndexOf(roleMarkPriority, accessory.GetMyIndex()) < Array.IndexOf(roleMarkPriority, accessory.GetIndexInParty(anotherOneId[0]));
                Vector3 _myStartPoint = isMeGoLeft ? burningChainsLeft_template  :burningChainsRight_template;
                myStartPoint = Util.RotatePointInFFXIVCoordinate(_myStartPoint,originPos,newNorthRot);
            }
            else
            {
                
                List<uint> noburningChainsPlayers = accessory.Data.PartyList.Except(boss3_burningChainsPlayers).ToList();
                List<uint> anotherOneId = noburningChainsPlayers.Except(new List<uint>{accessory.Data.Me}).ToList();
                isMeGoLeft = anotherOneId.Count > 0 && Array.IndexOf(roleMarkPriority, accessory.GetMyIndex()) < Array.IndexOf(roleMarkPriority, accessory.GetIndexInParty(anotherOneId[0]));
                bool isBullEyeNeedChange = false;
                uint eyeStatusId = 3742;
                List<uint> bullEyePlayers = accessory.whoGetStatusInParty(eyeStatusId);
                isBullEyeNeedChange = bullEyePlayers.Count == 2 
                    && (
                        accessory.GetIndexInParty(bullEyePlayers[0]) + accessory.GetIndexInParty(bullEyePlayers[0]) == 1 
                        || accessory.GetIndexInParty(bullEyePlayers[0]) + accessory.GetIndexInParty(bullEyePlayers[0]) == 5
                        );
                isMeGoLeft = isBullEyeNeedChange ? !isMeGoLeft : isMeGoLeft;
                Vector3 _myStartPoint = isMeGoLeft ? noBurningChainsLeft_template : noBurningChainsRight_template;
                myStartPoint = Util.RotatePointInFFXIVCoordinate(_myStartPoint,originPos,newNorthRot);
            }
            
            myEndPoint = Util.RotatePointInFFXIVCoordinate(myStartPoint,originPos,isFireBallClockwise ? 1.05f:-1.05f);
            //绘制第二轮飞镖指路
            List<float[]> myPointsList = new List<float[]>();
            myPointsList.Add(new float[]{originPos.X,originPos.Y,originPos.Z,0,3300});
            myPointsList.Add(new float[]{myStartPoint.X,myStartPoint.Y,myStartPoint.Z,0,5000});
            myPointsList.Add(new float[]{myEndPoint.X,myEndPoint.Y,myEndPoint.Z,0,5000});
            MultiDisDraw(myPointsList,accessory);
            accessory.Log.Debug($"Boss 3 Second Dart Board : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 3 Second Dart Board :isFireBallClockwise => {isFireBallClockwise}");
            accessory.Log.Debug($"Boss 3 Second Dart Board :isMeGetBurningChains => {isMeGetBurningChains}");
            accessory.Log.Debug($"Boss 3 Second Dart Board :isMeGoLeft => {isMeGoLeft}");
            accessory.Log.Debug($"Boss 3 Second Dart Board :newNorthRot => {newNorthRot}");
        }

        //以火箭出现作为指路开始的时机
        // 惊喜导弹16482 Tether 0011 
        // 惊喜爪 16484
        // 惊喜杖 16483
        // 导弹和爪可以通过.TargetObject来获得连线的目标




        [ScriptMethod(name: "Boss 3 运动会 Boss 3 Surprising Claw Phase", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16484|16492)$"])]
        public async void Boss3_SurprisingClawPhase(Event @event, ScriptAccessory accessory)
        {
            
            if(IsInSuppress(10000,nameof(Boss3_SurprisingClawPhase)))
            {
                return;
            }
            if(await DelayMillisecond(1000))
            {
                return;
            }

            accessory.Log.Debug($"Actived! => Boss 3 Surprising Claw Phase");

            
            
            uint surprisingClawDataId = @event.GetDataId();
            //异闻难度的导弹DataId为16482, 零式异闻难度为16490;
            uint surprisingMissileDataId = surprisingClawDataId == 16484 ? (uint)16482 : (uint)16490;
            // List<uint> clawPlayers = new List<uint>();
            // List<uint> missilePlayers = new List<uint>();
            bool isMeGetClaw = false;
            bool isMyTargetAtLeft = false;
            Vector3 originPos = new (-200,-200,0);

            foreach (IGameObject _claw in accessory.GetEntitiesByDataId(surprisingClawDataId))
            {
                if(_claw.TargetObjectId > 0x10000000)
                {
                    if(_claw.TargetObjectId == accessory.Data.Me)
                    {
                        isMeGetClaw = true;
                        Vector3 _pos = Util.RotatePointInFFXIVCoordinate(_claw.Position,originPos, - 0.5f * MathF.PI - boss3_rad_distance[0][0]);
                        isMyTargetAtLeft = _pos.X < originPos.X;
                    }
                    // clawPlayers.Add((uint)_claw.TargetObjectId);
                }
            }
            foreach (IGameObject _missile in accessory.GetEntitiesByDataId(surprisingMissileDataId))
            {
                if(_missile.TargetObjectId > 0x10000000)
                {
                    if(_missile.TargetObjectId == accessory.Data.Me)
                    {
                        Vector3 _pos = Util.RotatePointInFFXIVCoordinate(_missile.Position,originPos,- 0.5f * MathF.PI - boss3_rad_distance[0][0]);
                        isMyTargetAtLeft = _pos.X < originPos.X;
                    }
                    // missilePlayers.Add((uint)_missile.TargetObjectId);
                }
            }

            //模板以1点 -0.25π 为新北点
            Vector3 claw_startTemplate = new (-200 + 13,-200, -13);
            Vector3 claw_endTemplate = new (-200 - 13,-200, 13);
            Vector3 missile_startTemplate = new (-200,-200,0);
            Vector3 missile_endTemplateLeft = new (-200 - 13 ,-200, -13);
            Vector3 missile_endTemplateRight = new (-200 + 13 ,-200, 13);

            //绘制,如果是爪子则添加一个中途导航点
            List<float[]> myPointsList = new List<float[]>();

            float _rot = boss3_rad_distance[0][0] + 0.25f * MathF.PI;
            Vector3 _myStartPoint = isMeGetClaw ? claw_startTemplate : missile_startTemplate;
            Vector3 myStartPoint = Util.RotatePointInFFXIVCoordinate(_myStartPoint,originPos,_rot);
            Vector3 _myEndPoint = isMeGetClaw ? claw_endTemplate : missile_endTemplateLeft;
            Vector3 myEndPoint = Util.RotatePointInFFXIVCoordinate(_myEndPoint,originPos,_rot);
            if((!isMeGetClaw)&&isMyTargetAtLeft)
            {
                _myEndPoint = missile_endTemplateRight;
                myEndPoint =  Util.RotatePointInFFXIVCoordinate(_myEndPoint,originPos,_rot);
            }
            //添加起始点
            myPointsList.Add(new float[]{myStartPoint.X,myStartPoint.Y,myStartPoint.Z,0,7500});
            //如果是爪则添加拐点
            if(isMeGetClaw){
                myPointsList[0][4] = 3800;
                //添加拐点
                Vector3 _point = Util.RotatePointInFFXIVCoordinate(myStartPoint,originPos,isMyTargetAtLeft?0.3f * MathF.PI:-0.3f * MathF.PI);
                myPointsList.Add(new float[]{_point.X,_point.Y,_point.Z,0,3000});
            }
            //添加结束点
            myPointsList.Add(new float[]{myEndPoint.X,myEndPoint.Y,myEndPoint.Z,0,4000});
            MultiDisDraw(myPointsList,accessory);
            accessory.Log.Debug($"Boss 3 Surprising Claw Phase : my order number in party => {1 + accessory.GetMyIndex()}");
            accessory.Log.Debug($"Boss 3 Surprising Claw Phase :isMeGetClaw => {isMeGetClaw}");
            accessory.Log.Debug($"Boss 3 Surprising Claw Phase :isMyTargetAtLeft => {isMyTargetAtLeft}");
            accessory.Log.Debug($"Boss 3 Surprising Claw Phase :new North => {boss3_rad_distance[0][0]}");
        }
        
        [ScriptMethod(name: "Boss 3 开心扳机安全区 Boss 3 Trigger Happy Safe Zone", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35207|35208)$"])]
        public void Boss3_TriggerHappySafeZone(Event @event, ScriptAccessory accessory)
        {
            //安全区为一个读条35207的boss分身,面向的一个60度扇形
            DrawPropertiesEdit dpFan = accessory.GetDrawPropertiesEdit(@event.GetSourceId(), new(20), 4600, true);
            dpFan.Radian = MathF.PI / 3.0f;
            dpFan.Color = accessory.Data.DefaultSafeColor.WithW(3.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpFan);

        }

        /*
            移动命令 前 3538
            移动命令 后 3539
            移动命令 左 3540
            移动命令 右 3541
            
        */
        [ScriptMethod(name: "Boss 3 移动命令目的地提示 Boss 3 Forward March Hint ", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3538|3539|3540|3541)$"])]
        public void Boss3_ForwardMarchHint(Event @event, ScriptAccessory accessory)
        {

            if(@event.GetTargetId() != accessory.Data.Me)
            {
                return;
            }
            float[] rad = new float[]
            {
                0,
                MathF.PI,
                0.5f * MathF.PI,
                -0.5f * MathF.PI
            };

            DrawPropertiesEdit dpDis = accessory.GetDrawPropertiesEdit(accessory.Data.Me,new (0.7f,11.5f),5900,false);
            dpDis.Rotation = rad[@event.GetStatusID() - 3538];
            dpDis.Delay = @event.GetDurationMilliseconds() - 6000;
            dpDis.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dpDis);
        
        }

        //标记飞针DataId16479的AOE范围
        [ScriptMethod(name: "Boss 3 飞针 Boss 3 Needles", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16479|16487)$"])]
        public void Boss3_Needles(Event @event, ScriptAccessory accessory)
        {
           accessory.FastDraw(DrawTypeEnum.Rect,@event.GetSourceId(),new(2, 40),new (5000,6000),false);
        }
        #endregion


        // [ScriptMethod(name: "Test", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo","Message:test"]), userControl: false]
        // public void Test(Event @event, ScriptAccessory accessory)
        // {
        //     accessory.Log.Debug($"Test");            

        // }
        private void MultiDisDraw(List<float[]> pointsList, ScriptAccessory accessory)
        {
            Vector4 colorGoNow = GuideColor_GoNow.V4.WithW(GuideColorDensity);
            Vector4 colorGoLater = GuideColor_GoLater.V4.WithW(GuideColorDensity);
            if(boss3_fireSpreadRotation.Count == 3){
                colorGoNow = colorGoNow.WithW(colorGoNow.W + 8);
                colorGoLater = colorGoLater.WithW(colorGoLater.W + 8);
            }
            accessory.DrawWaypoints(pointsList,true,0,colorGoNow,colorGoLater);
        }

        private bool IsInSuppress(int suppressMillisecond,string methodName) {
            lock(_lock){
                return TsingUtilities.IsInSuppress(invokeTimestamp, methodName, suppressMillisecond);
            }
        }

        private async Task<bool> DelayMillisecond(int delayMillisecond){
            return await TsingUtilities.DelayMillisecond(delayMillisecond,cancellationTokenSource.Token);
        }


    }








    #region 拓展方法
    public static class ScriptExtensions_Tsing
    {

        //获取id
        private static bool ParseHexId(string? idStr, out uint id)
        {
            id = 0;
            if (string.IsNullOrEmpty(idStr)) return false;
            try
            {
                var idStr2 = idStr.Replace("0x", "");
                id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static uint GetActionId(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
        }
        public static uint GetSourceId(this Event @event)
        {
            return ParseHexId(@event["SourceId"], out uint id) ? id : 0;
        }
        public static uint GetSourceDataId(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
        }
        public static uint GetDataId(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
        }

        public static uint GetTargetId(this Event @event)
        {
            return ParseHexId(@event["TargetId"], out uint id) ? id : 0;
        }

        public static uint GetTargetIndex(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
        }

        public static Vector3 GetSourcePosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        }

        public static Vector3 GetTargetPosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
        }

        public static Vector3 GetEffectPosition(this Event @event)
        {
            return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
        }

        public static float GetSourceRotation(this Event @event)
        {
            return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
        }

        public static float GetTargetRotation(this Event @event)
        {
            return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
        }

        public static string GetSourceName(this Event @event)
        {
            return @event["SourceName"];
        }

        public static string GetTargetName(this Event @event)
        {
            return @event["TargetName"];
        }

        public static uint GetDurationMilliseconds(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
        }

        public static uint GetIndex(this Event @event)
        {
            return ParseHexId(@event["Index"], out uint id) ? id : 0;
        }

        public static uint GetState(this Event @event)
        {
            return ParseHexId(@event["State"], out uint id) ? id : 0;
        }

        public static uint GetDirectorId(this Event @event)
        {
            return ParseHexId(@event["DirectorId"], out uint id) ? id : 0;
        }

        public static uint GetStatusID(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
        }

        public static uint GetStackCount(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
        }

        public static uint GetParam(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["Param"]);
        }

        public static uint GetIconId(this Event @event)
        {
            return ParseHexId(@event["Id"], out uint id) ? id : 0;
        }





        //获取小队成员的队伍序号
        public static int GetIndexInParty (this ScriptAccessory accessory,uint entityId){
            return accessory.Data.PartyList.IndexOf(entityId);
        }
        public static int GetMyIndex(this ScriptAccessory accessory){
            return accessory.GetIndexInParty(accessory.Data.Me);
        }



        //获取小队列表
        public static IEnumerable<IBattleChara> GetPartyEntities(this ScriptAccessory accessory){
            return accessory.Data.Objects.Where(obj => obj is IBattleChara && accessory.Data.PartyList.Contains(obj.EntityId)).Select(obj => (IBattleChara)obj);
        }

        public static IEnumerable<IGameObject> GetEntitiesByDataId(this ScriptAccessory accessory,uint dataId){
            return accessory.Data.Objects.Where(obj => obj is IGameObject && obj?.DataId == dataId);
        }
        public static List<uint> GetEntityIdsByDataId(this ScriptAccessory accessory,uint dataId){
            return accessory.GetEntitiesByDataId(dataId).Select(obj => (obj?.EntityId) ?? 0).ToList();
        }
        public static Dictionary<uint,IGameObject?> GetEntitiesByIdsList(this ScriptAccessory accessory,List<uint> idsList)
        {
            Dictionary<uint,IGameObject?> dict = new Dictionary<uint,IGameObject?>();
            foreach (uint id in idsList){
                dict[id] = accessory.Data.Objects.SearchByEntityId(id);
            }
            return dict;
        }

        //获取某个特定实体某个特定status的信息
        public static Status? GetStatusInfo(this ScriptAccessory accessory,uint entityId, uint statusId)
        {
            Status statusInfo = null;

            if(accessory.Data.Objects.SearchByEntityId(entityId) is IBattleChara entityObject)
            {
                foreach (Status status in entityObject.StatusList)
                {
                    if (status.StatusId == statusId)
                    {
                        statusInfo = status;
                        break;
                    }
                }
            }
            return statusInfo;
        }




        
        //根据statusId获得持有该status的实体的列表
        public static List<uint> whoGetStatus(this ScriptAccessory accessory,uint statusId)
        {
            List<uint> objectIdsList = new List<uint>();
            foreach (IGameObject entityObject in accessory.Data.Objects)
            {  //IBattleChara
                //subKind 4(玩家) 9(战斗NPC) 5,11(敌对怪物)
                if (Array.Exists(new byte[] { 4, 9, 5, 11 }, subKind => subKind == entityObject.SubKind))
                {
                    if(accessory.GetStatusInfo(entityObject.EntityId,statusId) != null)
                    {
                        objectIdsList.Add(entityObject.EntityId);
                    }
                }
            }
            return objectIdsList;
        }
        public static bool isEntityGetStatus(this ScriptAccessory accessory,uint entityId,uint statusId)
        {
            return (accessory.whoGetStatus(statusId) ?? new List<uint>()).Contains(entityId);
        }
        public static bool isMeGetStatus(this ScriptAccessory accessory,uint statusId)
        {
            return accessory.isEntityGetStatus(accessory.Data.Me,statusId);
        }
        public static List<uint> whoGetStatusInParty(this ScriptAccessory accessory,uint statusId)
        {
            return accessory.whoGetStatus(statusId).Intersect(accessory.Data.PartyList).ToList();
        }
        public static List<uint> whoGetStatusInPartyWithoutMe(this ScriptAccessory accessory,uint statusId)
        {
            return accessory.whoGetStatusInParty(statusId).Except(new List<uint> { accessory.Data.Me }).ToList();
        }
        public static List<uint> whoNotGetStatusInParty(this ScriptAccessory accessory,uint statusId)
        {
            return accessory.Data.PartyList.Except(accessory.whoGetStatusInParty(statusId)).ToList();
        }
        public static List<uint> whoNotGetStatusInPartyWithoutMe(this ScriptAccessory accessory,uint statusId)
        {
            return accessory.whoNotGetStatusInParty(statusId).Except(new List<uint> { accessory.Data.Me }).ToList();
        }




        //颜色, 颜色内部的4个参数 R,G,B,density(颜色浓度,非透明度)
        public enum ColorType {
            Red,Pink,Cyan,Orange
        }
        private static readonly Dictionary<ColorType, ScriptColor> colors = new Dictionary<ColorType, ScriptColor>
        {
            { ColorType.Red, new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) } },
            { ColorType.Pink, new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) } },
            { ColorType.Cyan, new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) } },
            { ColorType.Orange, new ScriptColor { V4 = new Vector4(1f, 0.8f, 0f, 1.5f) } },
        };
        public static ScriptColor GetColor(this ScriptAccessory accessory, ColorType colorType)
        {
            return colors[colorType];
        }


        //快捷绘图参数和快捷绘图

        public static DrawPropertiesEdit GetDrawPropertiesEdit(this ScriptAccessory accessory,string name,object position, Vector2 scale, long delay, long destoryAt,Vector4 color)
        {
            DrawPropertiesEdit drawPropertiesEdit= accessory.Data.GetDefaultDrawProperties();
            drawPropertiesEdit.Name = name;
            switch (position)
            {
                case Vector3 position_v3:
                    drawPropertiesEdit.Position = position_v3;
                    break;
                case uint position_id:
                    drawPropertiesEdit.Owner = position_id;
                    break;
                default:
                    accessory.Log.Debug($"parm type error : position =>{position}");
                    break;
            }


            drawPropertiesEdit.Scale = scale;
            drawPropertiesEdit.Delay = delay;
            drawPropertiesEdit.DestoryAt = destoryAt;
            drawPropertiesEdit.Color = color;
            //drawPropertiesEdit.TargetColor = targetColor;
            return drawPropertiesEdit;
        }


        public static DrawPropertiesEdit GetDrawPropertiesEdit(this ScriptAccessory accessory,object position, Vector2 scale, long destoryAt, bool isSafe)
        {
            return accessory.GetDrawPropertiesEdit(Guid.NewGuid().ToString(),position,scale,0,destoryAt
            ,isSafe?accessory.Data.DefaultSafeColor:accessory.Data.DefaultDangerColor);
        }


        public static void FastDraw(this ScriptAccessory accessory,DrawTypeEnum drawType,object position,Vector2 scale, Vector2 delay_destoryAt, bool isSafe){
            DrawPropertiesEdit drawPropertiesEdit = accessory.GetDrawPropertiesEdit(position,scale,(long)delay_destoryAt.Y,isSafe);
            drawPropertiesEdit.Delay = (long)delay_destoryAt.X;
            if(drawType == DrawTypeEnum.Displacement && (position is Vector3)){
                //如果是画指向, 且postion为Vector3
                drawPropertiesEdit.Owner = accessory.Data.Me;
                drawPropertiesEdit.Position = null;
                drawPropertiesEdit.ScaleMode |= ScaleMode.YByDistance;
                drawPropertiesEdit.TargetPosition = (Vector3)position;
            }
            //accessory.Log.Debug($"FastDraw {drawType.ToString()} :{drawPropertiesEdit.ToString()}");
            accessory.Method.SendDraw(DrawModeEnum.Default, drawType, drawPropertiesEdit);
        }

        public static void FastDraw(this ScriptAccessory accessory,DrawTypeEnum drawType,object position,Vector2 scale, Vector2 delay_destoryAt, Vector4 color){
            DrawPropertiesEdit drawPropertiesEdit = accessory.GetDrawPropertiesEdit(position,scale,(long)delay_destoryAt.Y,true);
            drawPropertiesEdit.Delay = (long)delay_destoryAt.X;
            if(drawType == DrawTypeEnum.Displacement && (position is Vector3)){
                //如果是画指向, 且postion为Vector3
                drawPropertiesEdit.Owner = accessory.Data.Me;
                drawPropertiesEdit.Position = null;
                drawPropertiesEdit.ScaleMode |= ScaleMode.YByDistance;
                drawPropertiesEdit.TargetPosition = (Vector3)position;
            }
            drawPropertiesEdit.Color = color;
            //accessory.Log.Debug($"FastDraw {drawType.ToString()} :{drawPropertiesEdit.ToString()}");
            accessory.Method.SendDraw(DrawModeEnum.Default, drawType, drawPropertiesEdit);
        }




        public static void FastDrawDisplacement(this ScriptAccessory accessory,Vector3[] twoPosition,Vector2 scale, long destoryAt, Vector4 color)
        {
            if(twoPosition.Length == 2){
                DrawPropertiesEdit drawPropertiesEdit = accessory.GetDrawPropertiesEdit(twoPosition[0],scale,destoryAt,true);
                drawPropertiesEdit.TargetPosition = twoPosition[1];
                drawPropertiesEdit.Color = color;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, drawPropertiesEdit);
            }
        }
        public static void DrawWaypoints(this ScriptAccessory accessory,List<float[]> pointsList,bool circleAtPoint, long baseDelayMillis, Vector4 color_goNow, Vector4 color_goLater)
        {
            long guideStartTimeMillis = baseDelayMillis;
            string guid = Guid.NewGuid().ToString();
            for (int i = 0; i < pointsList.Count; i++)
            {
                if(pointsList[i].Length < 5){
                    accessory.Log.Debug($"pointsList[{i}]’s length < 5");
                    break;
                }
                int count = 0;
                string name = $"DrawWaypoints go now {i} : {guid}";

                //go now 部分
                DrawPropertiesEdit drawPropertiesEdit_goNow = accessory.GetDrawPropertiesEdit(
                    name + count++
                    ,accessory.Data.Me
                    ,new (1.5f)
                    ,guideStartTimeMillis + (int)pointsList[i][3] - Math.Sign(i) * 270
                    ,(int)pointsList[i][4] - 100
                    ,color_goNow);
                drawPropertiesEdit_goNow.ScaleMode |= ScaleMode.YByDistance;
                drawPropertiesEdit_goNow.TargetPosition = new Vector3(pointsList[i][0], pointsList[i][1], pointsList[i][2]);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, drawPropertiesEdit_goNow);
            
                if(circleAtPoint)
                {
                    DrawPropertiesEdit drawPropertiesEdit_goNowEndCircle = accessory.GetDrawPropertiesEdit(
                    name + count++
                    ,drawPropertiesEdit_goNow.TargetPosition
                    ,new((float)(0.2 + 0.5 * drawPropertiesEdit_goNow.Scale.X))
                    ,drawPropertiesEdit_goNow.Delay
                    ,drawPropertiesEdit_goNow.DestoryAt
                    ,color_goNow);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, drawPropertiesEdit_goNowEndCircle);
                }
                //如果当前点位不是第一个点位，则进行go later部分
                if( i >= 1)
                {
                    DrawPropertiesEdit drawPropertiesEdit_goLater = accessory.GetDrawPropertiesEdit(
                        name + count++
                        ,new Vector3 (pointsList[i - 1][0], pointsList[i - 1][1], pointsList[i - 1][2])
                        ,new (1.5f)
                        ,baseDelayMillis + (int)pointsList[0][3]
                        ,guideStartTimeMillis - (baseDelayMillis + (int)pointsList[0][3]) - 100
                        ,color_goLater);
                    drawPropertiesEdit_goLater.TargetPosition = new Vector3 (pointsList[i][0], pointsList[i][1], pointsList[i][2]);
                    drawPropertiesEdit_goLater.ScaleMode |= ScaleMode.YByDistance;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, drawPropertiesEdit_goLater);

                    if(circleAtPoint)
                    {
                        DrawPropertiesEdit drawPropertiesEdit_goLaterEndCircle = accessory.GetDrawPropertiesEdit(
                        name + count++
                        ,drawPropertiesEdit_goLater.TargetPosition
                        ,new((float)(0.2 + 0.5 * drawPropertiesEdit_goLater.Scale.X))
                        ,drawPropertiesEdit_goLater.Delay
                        ,drawPropertiesEdit_goLater.DestoryAt - 100
                        ,color_goLater);
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, drawPropertiesEdit_goLaterEndCircle);
                    }

                }
                //为下一个点准备开始时间节点(guideStartTimeMillis为当前点全部指路的结束时间点)
                guideStartTimeMillis = guideStartTimeMillis + (int)pointsList[i][3] + (int)pointsList[i][4];
            }
        }


        /*
        画一组面向机制的指示
        1.由最终面向目标区域指示+当前面向指示两部分组成
        2.最终面向目标区域，由 1个donut绘图+1个fan绘图+2个line绘图 组成，像一块披萨，但是中部被挖去一个扇形
        3.当前面向指示由1个donut绘图组成，构成上述中披萨被挖去的中部扇形部分
        4.是否可以做到当前面向的扇形颜色，在处在安全区和不处在安全区时，两种不同的颜色?
        */ 
        public static void DrawTurnTowards(this ScriptAccessory accessory,object position,Vector3 towardsDonutScale_radAndRotation, Vector2 palyerDonutScale,Vector2 delay_destoryAt,Vector4 color,bool palyerDonutOn)
        {
            //1.绘制中缺披萨部分
            //KOD中的弧度增方向似乎尊重笛卡尔坐标系中的弧度增方向，与狒狒坐标系中的弧度增方向相反
            Vector3 pizzaDp = towardsDonutScale_radAndRotation;
            DrawPropertiesEdit dptt1 = accessory.GetDrawPropertiesEdit(position,new (pizzaDp.X),(long)delay_destoryAt.Y,true);
            dptt1.Owner = accessory.Data.Me;
            dptt1.Delay = (long)delay_destoryAt.X;
            dptt1.InnerScale = new (palyerDonutScale.X);
            dptt1.Radian = pizzaDp.Y;
            dptt1.Rotation = - pizzaDp.Z;
            dptt1.Color = color;
            switch (position)
            {
                case Vector3 position_v3:
                    dptt1.Position = null;
                    dptt1.TargetPosition = position_v3;
                    break;
                case uint position_id:
                    dptt1.TargetObject = position_id;
                    break;
                default:
                    accessory.Log.Debug($"parm type error : position =>{position}");
                    break;
            }
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dptt1);
            dptt1.Scale = new (palyerDonutScale.Y);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dptt1);
            dptt1.Scale = new (2,pizzaDp.X);
            dptt1.Rotation = - pizzaDp.Z + 0.5f * pizzaDp.Y;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dptt1);
            dptt1.Rotation = - pizzaDp.Z - 0.5f * pizzaDp.Y;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dptt1);

            //绘制人物面向扇形部分, 有时候有一个机制由多个引导面向
            if(palyerDonutOn){
                DrawPropertiesEdit dptt2 = accessory.GetDrawPropertiesEdit(accessory.Data.Me,new (palyerDonutScale.X),(long)delay_destoryAt.Y,true);
                dptt2.Delay = (long)delay_destoryAt.X;
                dptt2.InnerScale = new (palyerDonutScale.Y);
                dptt2.Radian = pizzaDp.Y - 0.02f;
                dptt2.Color = color;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dptt2);
            }


        }


    
    }
    #endregion



    #region 工具类
    public static class TsingUtilities
    {

        
        public static float DistanceBetweenTwoPoints(Vector3 point1,Vector3 point2)
        {
            float x = point1.X - point2.X;
            float y = point1.Y - point2.Y;
            float z = point1.Z - point2.Z;
            return MathF.Sqrt(MathF.Pow(x, 2) + MathF.Pow(y, 2) + MathF.Pow(z, 2));
        }

        public static float DistanceBetweenTwoPoints(Vector2 point1,Vector2 point2)
        {
            float x = point1.X - point2.X;
            float y = point1.Y - point2.Y;
            return MathF.Sqrt(MathF.Pow(x, 2) + MathF.Pow(y, 2));
        }

        


        //在狒狒的坐标系中旋转角度,狒狒坐标系中的顺时针方向弧度值增大，逆时针方向弧度值减少
        /*
        狒狒中的坐标系与笛卡尔坐标系不同，
        狒狒十四    中的x轴方向为从左向右,y轴方向为从上向下(游戏中坐标的第三个参数为纵坐标,不是垂直坐标)
        笛卡尔坐标系中的x轴方向为从左向右,y轴方向为从下向上
        也就是说在狒狒十四中,象限分布为
        第三象限 | 第四象限
        ------------------
        第二象限 | 第一象限
        笛卡尔坐标系的顺时针，和狒狒十四坐标系中的顺时针相反 <= 由于Y轴被反转了
        */


        public static Vector2 RotatePoint(Vector2 point, Vector2 centre, float radian)
        {
            Vector2 centreToPoint_v2 = new(point.X - centre.X, point.Y - centre.Y);
            float rot = (MathF.Atan2(centreToPoint_v2.Y, centreToPoint_v2.X) + radian);
            float length = centreToPoint_v2.Length();
            return new(centre.X + MathF.Cos(rot) * length, centre.Y + MathF.Sin(rot) * length);
        }
        public static Vector2 RotatePoint(Vector2 point, Vector2 centre, double radian)
        {
            return RotatePoint(point, centre, (float)radian);
        }
        public static Vector2 RotatePoint(Vector2 point, float radian)
        {
            return RotatePoint(point, new(0,0),radian);
        }
        public static Vector2 RotatePoint(Vector2 point,double radian)
        {
            return RotatePoint(point, new(0,0),radian);
        }


        public static Vector3 RotatePointInFFXIVCoordinate(Vector3 point, Vector3 centre, double radian)
        {
            Vector2 centreToPoint_v2 = new(point.X - centre.X, point.Z - centre.Z);
            float rot = (float)(MathF.Atan2(centreToPoint_v2.Y, centreToPoint_v2.X) + radian);
            float length = centreToPoint_v2.Length();
            return new(centre.X + MathF.Cos(rot) * length, centre.Y, centre.Z + MathF.Sin(rot) * length);
        }
        public static Vector3 RotatePointInFFXIVCoordinate(Vector3 point, float[] centre, double radian)
        {
            Vector3 centre_v3 = new(centre[0], centre[1], centre[2]);
            return RotatePointInFFXIVCoordinate(point, centre_v3, radian);
        }
        public static float[] RotatePointInFFXIVCoordinate(float[] point, Vector3 centre, double radian)
        {
            Vector3 point_v3 = new(point[0], point[1], point[2]);
            Vector3 resultPoint = RotatePointInFFXIVCoordinate(point_v3, centre, radian);
            return new float[] { resultPoint.X, resultPoint.Y, resultPoint.Z };
        }
        public static float[] RotatePointInFFXIVCoordinate(float[] point, float[] centre, double radian)
        {
            Vector3 point_v3 = new(point[0], point[1], point[2]);
            Vector3 centre_v3 = new(centre[0], centre[1], centre[2]);
            Vector3 resultPoint = RotatePointInFFXIVCoordinate(point_v3, centre_v3, radian);
            return new float[] { resultPoint.X, resultPoint.Y, resultPoint.Z };
        }




        //点做轴对称

        public static Vector2 AxisymmetricPoint(Vector2 point, float rot)
        {
            float rotPoint = MathF.Atan2(point.Y,point.X);
            float radian = 2 * rot - 2 * rotPoint;
            return RotatePoint(point,new Vector2(0,0),radian);
        }
        public static Vector2 AxisymmetricPoint(Vector2 point, double rot)
        {
            return AxisymmetricPoint(point, (float)rot);
        }
        
        public static Vector2 AxisymmetricPoint(Vector2 point, Vector2 axis)
        {
            return AxisymmetricPoint(point, MathF.Atan2(axis.Y,axis.X));
        }

        public static Vector2 AxisymmetricPointByX(Vector2 point)
        {
            return AxisymmetricPoint(point,0);
        }

        public static Vector2 AxisymmetricPointByY(Vector2 point)
        {
            return AxisymmetricPoint(point,Math.PI);
        }
        

        public static Vector3 AxisymmetricPointInFFXIVCoordinate(Vector3 point, Vector3 axis)
        {
            Vector2 point_v2 = new(point.X, point.Z);
            Vector2 axis_v2 = new(axis.X, axis.Z);
            Vector2 resultPoint_v2 = AxisymmetricPoint(point_v2, axis_v2);
            return new(resultPoint_v2.X, point.Y, resultPoint_v2.Y);
        }

        public static Vector3 AxisymmetricPointInFFXIVCoordinate(Vector3 point, float[] axis)
        {
            Vector3 axis_v3 = new(axis[0], axis[1], axis[2]);
            return AxisymmetricPointInFFXIVCoordinate(point, axis_v3);
        }

        public static float[] AxisymmetricPointInFFXIVCoordinate(float[] point, Vector3 axis)
        {
            Vector3 point_v3 = new(point[0], point[1], point[2]);
            Vector3 resultPoint = AxisymmetricPointInFFXIVCoordinate(point_v3, axis);
            return new float[] { resultPoint.X, resultPoint.Y, resultPoint.Z };
        }
        public static float[] AxisymmetricPointInFFXIVCoordinate(float[] point, float[] axis)
        {
            Vector3 point_v3 = new(point[0], point[1], point[2]);
            Vector3 axis_v3 = new(axis[0], axis[1], axis[2]);
            Vector3 resultPoint = AxisymmetricPointInFFXIVCoordinate(point_v3, axis_v3);
            return new float[] { resultPoint.X, resultPoint.Y, resultPoint.Z };
        }


        //做一个触发器CD
        // 当使用async和await关键字时，编译器会生成一个状态机以处理异步操作。这会导致在调试和运行时，方法名称会有所变化。请尽量【不要】使用MethodBase.GetCurrentMethod().Name作为获得方法名的方法。
        public static bool IsInSuppress(ConcurrentDictionary<string,long> dict, string methodName,long suppressMillisecond) {
            //1.获取上次触发的时间戳
            //2.对比当前时间戳
            //3.如果时间差大于暂停时间，说明不在暂停时间内,返回false
            //4.如果时间差小于或者等于暂停时间，说明在暂停时间内,返回true
            bool isIn = false;

            if (dict.TryGetValue(methodName, out long result))
            {
                //如果找到了上次触发的时间戳，则提取比对
                isIn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - result <= suppressMillisecond;

                //如果不在暂停期,则更新触发时间戳
                if(!isIn){
                    dict[methodName] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            else 
            {
                //如果没找到上次触发的时间戳，则填入一个时间戳记录
                //由于通常用在并发环境下
                //如果没找到，且无法添加，说明有别的并发先完成了添加,该并发作废
                isIn = !dict.TryAdd(methodName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            }

            return isIn;
        }

        //做一个有中断功能的延时器, 当被打断时, 会返回true;
        public static async Task<bool> DelayMillisecond(int delayMillisecond, CancellationToken cancellationToken){

            try{
                //等待 delayMillisecond
                await Task.Delay(delayMillisecond,cancellationToken);
                return false;
            }catch(TaskCanceledException){
                //如果被cancelled取消了
                return true;
            }catch(Exception){
                //被其他意外取消了
                return true;
            }

        }



    }
    #endregion


}