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
using System.Reflection;


using EX = TsingNamespace.TsingTest.ScriptExtensions_Tsing;





namespace TsingNamespace.TsingTest
{
    [ScriptType(name: "测试工具", guid: "e3cfc380-edc2-f441-2424-e9e294f2632e", version: "0.0.0.3", author: "Mao",note: noteStr)]
    public class TsingTestScript
    {
        const string noteStr =
        """
        1.面向修正指令
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

        [ScriptMethod(name: "获得某些debuff时进行面向修正", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23]|371[5-8])$"])]
        public async void TowardsRoundByStatusId(Event @event, ScriptAccessory accessory)
        {
            if(@event.GetTargetId() != accessory.Data.Me)
            {
                return;
            }
            uint duration = @event.GetDurationMilliseconds();
            if(duration > 1800000)
            {
                //传入了一个相当大的值,这可能是因为检测buff为永续buff所导致的
                uint statusId = @event.GetStatusID();
                switch(statusId)
                {
                    case 3452:
                    case 3453:
                        //欧米茄小电视
                        duration = 9600;
                        break;
                }
            }

            duration = duration > 5000 ? duration : 5000;
            if(await DelayMillisecond((int)duration - 5000))
            {
                return;
            }
            _towardsRound(new(4,100,5000),accessory);
        }
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
            _towardsRound(new(count,per,duration),accessory);
        }




        [ScriptMethod(name: "Test", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo","Message:test"])]
        public void Test(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"Test");
            // PrintObject(typeof(ScriptMethod),accessory);
        }
        // [ScriptMethod(name: "攻击", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25859"])]
        // public async void ActionEffectTest(Event @event, ScriptAccessory accessory)
        // {
        // }


        //打印类
        private void PrintObject(Type _type,ScriptAccessory accessory)
        {
            Type type = _type;

            // 打印类名
            accessory.Log.Debug($"Class Name: {type.Name}");

            // 获取并打印字段
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            accessory.Log.Debug("Fields:");
            foreach (var field in fields)
            {
                accessory.Log.Debug($" - {field.Name} (Type: {field.FieldType.Name})");
            }

            // 获取并打印属性
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            accessory.Log.Debug("Properties:");
            foreach (var property in properties)
            {
                accessory.Log.Debug($" - {property.Name} (Type: {property.PropertyType.Name})");
            }

            // 获取并打印方法
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            accessory.Log.Debug("Methods:");
            foreach (var method in methods)
            {
                accessory.Log.Debug($" - {method.Name} (Return Type: {method.ReturnType.Name})");
            }
    
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
        private async void _towardsRound(Vector3 count_per_duration,ScriptAccessory accessory)
        {
            accessory.Log.Debug($"_towardsRound => {count_per_duration}");
            int directionCount = (int)Math.Round(count_per_duration.X);
            int per = (int)Math.Round(count_per_duration.Y);
            int duration =  (int)Math.Round(count_per_duration.Z);
            int times = duration / per ;
            for (int i = 0; i < times; i++)
            {
                IGameObject myGameObject = (IGameObject) accessory.Data.Objects.SearchByEntityId(accessory.Data.Me);
                float _rot = myGameObject.Rotation;
                int _rotCount = (int)Math.Round(_rot/(2 * MathF.PI / directionCount));
                float rot = _rotCount * (2 * MathF.PI / directionCount);
                unsafe
                {
                    FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* myGameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)myGameObject.Address;
                    myGameObjectStruct->SetRotation(rot);
                }
                if(await DelayMillisecond(per))
                {
                    return;
                }
            }
        }

    }

    #region 拓展方法
    public static class ScriptExtensions_Tsing
    {
        public enum SID : uint
        {
            OversampledWaveCannonLoadingR = 3452, // none->player, extra=0x0, cleaves right side 小电视打右侧(特效在右)
            OversampledWaveCannonLoadingL = 3453, // none->player, extra=0x0, cleaves left side 小电视打左侧(特效在左)
        }


        public static async Task<bool> DelayMillisecond(int delayMillisecond, CancellationToken cancellationToken)
        {
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
        public static bool IsInSuppress(ConcurrentDictionary<string,long> dict, string methodName,long suppressMillisecond)
        {
            bool isIn = false;
            if (dict.TryGetValue(methodName, out long result))
            {
                isIn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - result <= suppressMillisecond;
                if(!isIn){
                    dict[methodName] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            else 
            {
                isIn = !dict.TryAdd(methodName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            return isIn;
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
        public static uint GetTargetId(this Event @event)
        {
            return ParseHexId(@event["TargetId"], out uint id) ? id : 0;
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
        public static uint GetStatusID(this Event @event)
        {
            return JsonConvert.DeserializeObject<uint>(@event["StatusID"] ?? "0");
        }
    }  
    #endregion
}