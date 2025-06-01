using System;
using Newtonsoft.Json;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using Dalamud.Utility.Numerics;
using System.Reflection;


using EX = TsingNamespace.TsingTest.ScriptExtensions_Tsing;





namespace TsingNamespace.TsingTest
{
    [ScriptType(name: "[7.2]Mao的小工具", guid: "e3cfc380-edc2-f441-2424-e9e294f2632f", version: "0.0.0.4", author: "Mao",note: noteStr)]
    public class TsingTestScript
    {
        const string noteStr =
        """
        1.面向修正指令(人物移动时不生效)
        /e  _towardsRound  参数1,参数2,参数3
            参数1-方向切割的数量
            参数2-修正的间隔(最低90)
            参数3-持续的时间(最低100)
            示例 /e  _towardsRound  4,100,5000
                以4个方向为基准(通常是东西南北),每隔100毫秒,持续5000毫秒
                将当前面向修正到最近的正东正西正南正北中的一个
        """;
        /*
        如果你打开该文件并且尝试复制指令, 请从下方复制
                /e _towardsRound 4,100,5000
        */




        [UserSetting("P3启用小电视面向修正")]
        public bool P3_OversampledWaveCannonLoading { get; set; } = false;
        [UserSetting("P5启用小电视面向修正")]
        public bool P5_OversampledWaveCannonLoading { get; set; } = false;

        [ScriptMethod(name: "面向修正指令", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo","Message:regex:^_towardsRound\\s+(\\d+),(\\d+),(\\d+)$"])]
        public async void TowardsRoundByCommand(Event @event, ScriptAccessory accessory)
        {
            string input = @event.GetMessage();
            string pattern = @"^_towardsRound\s+(\d+),(\d+),(\d+)$";
            Match match = Regex.Match(input, pattern);
            int count = int.Parse(match.Groups[1].Value);
            count = count > 0 ? count : 4;
            int per = int.Parse(match.Groups[2].Value);
            per = per > 89 ? per : 90;
            int duration = int.Parse(match.Groups[3].Value);
            duration = duration > 99 ? duration : 100;
            if(IsInSuppress(duration,nameof(TowardsRoundByCommand)))
            {
                return;
            }
            accessory._towardsRound(new(count,per,duration), cancellationTokenSource.Token);
        }

        [ScriptMethod(name: "欧米茄获得小电视Debuff时进行面向修正",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: [EX.TOPData.Regexes.OversampledWaveCannonLoadingStatusID])]
        public async void TowardsRoundByStatusId(Event @event, ScriptAccessory accessory)
        {
            // TODO尝试只在P3启用
            if (@event.TargetId != (ulong)accessory.Data.Me)
            {
                return;
            }
            uint duration = @event.GetDurationMilliseconds();
            // 欧米茄的小电视debuff没有剩余时间
            if (duration > 1800000)
            {
                //传入了一个相当大的值,这可能是因为检测debuff为无剩余时间所导致的
                uint statusId = @event.StatusId;
                switch (statusId)
                {
                    case 3452:
                    case 3453:
                        //欧米茄小电视
                        duration = 9600;
                        break;
                }
            }
            // 检测P3 P5
            // 玩家小电视数量3个为P3，1个为P5
            uint loadingCount = 0;
            if (accessory.Data.PartyList is not null)
            {
                foreach (uint playerId in accessory.Data.PartyList)
                {
                    ulong id = (ulong)playerId;
                    IGameObject? playerObj = accessory.Data.Objects.SearchById(id);
                    if (playerObj is not null && playerObj is IBattleChara playerChara)
                    {
                        // 检测小电视状态
                        if (playerChara.HasStatus((uint)EX.TOPData.SID.OversampledWaveCannonLoadingR) ||
                            playerChara.HasStatus((uint)EX.TOPData.SID.OversampledWaveCannonLoadingL))
                        {
                            loadingCount++;
                        }
                    }
                }
            }
            switch (loadingCount)
            {
                case 0:
                    return;
                case 3:
                    if (!P3_OversampledWaveCannonLoading) return;
                    break;
                case 1:
                    if (!P5_OversampledWaveCannonLoading) return;
                    break;
            }

            // duration = duration > 5000 ? duration : 5000;
            duration = Math.Max(duration, 5000);
            if (await DelayMillisecond((int)duration - 5000)) return;
            accessory._towardsRound(new(4,100,5000), cancellationTokenSource.Token);
        }

        public void Init(ScriptAccessory accessory)
        {

            //清除触发时间戳记录+延时任务
            invokeTimestamp.Clear();
            cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
        }
        // 一个CancellationTokenSource 用于取消延时任务
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly object _lock = new object();
        // 用于存放触发器触发的时间戳
        private ConcurrentDictionary<string, long> invokeTimestamp = new ConcurrentDictionary<string, long>();
        //面向修正
        private async Task<bool> DelayMillisecond(int delayMillisecond){
            return await EX.DelayMillisecond(delayMillisecond,cancellationTokenSource.Token);
        }
        private bool IsInSuppress(int suppressMillisecond,string methodName) {
            lock(_lock){
                return EX.IsInSuppress(invokeTimestamp, methodName, suppressMillisecond);
            }
        }


    }

    #region 拓展方法
    public static class ScriptExtensions_Tsing
    {

        public static class TOPData
        {
            public static class Regexes
            {
                public const string OversampledWaveCannonLoadingStatusID = "StatusID:regex:^(345[23])$";
            }
            public enum SID : uint
            {
                OversampledWaveCannonLoadingR = 3452, // none->player, extra=0x0, cleaves right side 小电视打右侧(特效在右)
                OversampledWaveCannonLoadingL = 3453, // none->player, extra=0x0, cleaves left side 小电视打左侧(特效在左)
            }
        }



        public static async Task<bool> DelayMillisecond(int delayMillisecond, CancellationToken cancellationToken)
        {
            try
            {
                //等待 delayMillisecond
                await Task.Delay(delayMillisecond, cancellationToken);
                return false;
            }
            catch (TaskCanceledException)
            {
                //如果被cancelled取消了
                return true;
            }
            catch (Exception)
            {
                //被其他意外取消了
                return true;
            }
        }
        public static bool IsInSuppress(ConcurrentDictionary<string, long> dict, string methodName, long suppressMillisecond)
        {
            bool isIn = false;
            if (dict.TryGetValue(methodName, out long result))
            {
                isIn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - result <= suppressMillisecond;
                if (!isIn)
                {
                    dict[methodName] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            else
            {
                isIn = !dict.TryAdd(methodName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            return isIn;
        }
        public static bool IsMoving
        {
            get
            {

                bool isMoving = false;
                unsafe
                {
                    FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap* ptr = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
                    if (ptr is not null)
                    {
                        isMoving = ptr->IsPlayerMoving;
                    }
                }
                return isMoving;
            }
        }



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
        public static uint GetDurationMilliseconds(this Event @event)
        {
            string _dm = @event["DurationMilliseconds"];
            _dm = _dm.Length < 10 ? _dm : "404404404";
            return JsonConvert.DeserializeObject<uint>(_dm);
        }
        public static string GetMessage(this Event @event)
        {
            return @event["Message"];
        }
        
        public static async void _towardsRound(this ScriptAccessory accessory, (int Count, int Per, int Duration)count_per_duration, CancellationToken cancellationToken)
        {
            accessory.Log.Debug($"_towardsRound => {count_per_duration}");
            int directionCount = count_per_duration.Count;
            int per = count_per_duration.Per;
            int duration =  count_per_duration.Duration;
            int times = duration / per ;
            IPlayerCharacter myChara = accessory.Data.MyObject;
            if (myChara is null)
            {
                accessory.Log.Error("无法获取玩家角色对象");
                return;
            }
            string logStr = "";
            for (int i = 0; i < times; i++)
            {
                float _rot = myChara.Rotation;
                int _rotCount = (int)Math.Round(_rot / (2 * MathF.PI / directionCount));
                float rot = _rotCount * (2 * MathF.PI / directionCount);
                if (!EX.IsMoving)
                {
                    if (logStr != "玩家没有在移动,进行面向修正")
                    {
                        logStr = "玩家没有在移动,进行面向修正";
                        accessory.Log.Debug(logStr);
                    }
                    unsafe
                    {
                        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* myGameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)myChara.Address;
                        myGameObjectStruct->SetRotation(rot);
                    }
                }
                else
                { 
                    if (logStr != "玩家在移动, 等待移动停止")
                    { 
                        logStr = "玩家在移动, 等待移动停止";
                        accessory.Log.Debug(logStr);
                    }
                }
                if (await DelayMillisecond(per, cancellationToken)) return;
            }
        }
    }  
    #endregion
}
