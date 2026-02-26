using Newtonsoft.Json;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using KodakkuAssist.Script;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;


namespace TsingNamespace.Normal.LittleLadiesDay2026
{

    [ScriptType(name: "2026女儿节公演声援", territorys: [130], guid: "22cfc380-edc2-f441-f9f9-f9f212f2632f", version: "0.0.0.1", author: "Mao")]
    public class LittleLadiesDay
    {
        [UserSetting("公演观众状态续杯提示")]
        public bool FanStatusReminder { get; set; } = true;

        [UserSetting("公演观众状态续杯提示时使用语音提示")]
        public bool FanStatusReminderVoice { get; set; } = false;


        // 选中NPC /tnpc
        // 直接使用 accessory.Method.UseAction(0x4002264D, 44501);即可使用动作
        // 44501 小红
        // 44502 小黄
        // 44503 小蓝
        // 44504 小紫
        // 公演观众状态1494 ，四个都是，但是Param不同
        // 乌拉拉小红 561 dataId 18859
        // 鸣海 562 dataId 18860
        // 马夏·玛卡拉卡 563 dataId 18861
        // 皮科特 564 dataId 18862
        [ScriptMethod(name: "Little Ladies 切换为可选中状态",
            eventType: EventTypeEnum.Targetable,
            suppress : 1000,
            eventCondition: ["DataId:regex:^(18859|1886[0-2])$", "Targetable:True"])]
        public void TargetableTrue_LittleLadies(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"[TargetableTrue_LittleLadies] SourceId: {@event.SourceId}");
            ulong sourecId = @event.SourceId;
            Thread.Sleep(1000); // 稍微延迟一下，秒放太像挂了
            // 根据身上状态确认要选中哪个小偶像
            IPlayerCharacter myChara = accessory.Data.MyObject;
            if (myChara is null) return;
            Status statusInfo = null;
            
            uint fanStatusId = 1494;
            foreach (Status status in myChara.StatusList)
            {
                if (status.StatusId == fanStatusId)
                {
                    statusInfo = status;
                    break;
                }
            }
            if (statusInfo is null)
            {
                // 我没有公演观众的状态
                return;
            }

            // 根据状态的Param来判断选哪个小偶像
            uint npcDataId = statusInfo.Param switch
            {
                561 => 18859, // 乌拉拉
                562 => 18860, // 鸣海
                563 => 18861, // 马夏·玛卡拉卡
                564 => 18862, // 皮科特
                _ => 18859, // 默认声援拉拉肥
            };
            uint myActionId = npcDataId switch
            {
                18859 => 44501, // 乌拉拉小红
                18860 => 44502, // 鸣海小黄
                18861 => 44503, // 马夏·玛卡拉卡小蓝
                18862 => 44504, // 皮科特小紫
                _ => 44501, // 默认声援拉拉肥
            };
            // 查找小偶像的可选中状态
            IGameObject? targetNpc = accessory.Data.Objects.GetByDataId(npcDataId).FirstOrDefault(obj => obj.IsTargetable);
            if (targetNpc is not null)
            {
                // 我要声援的小偶像是可选中的
                // 选中一下目标，让你看起来没那么像挂
                accessory.Method.SelectTarget((uint)targetNpc.GameObjectId);
                accessory.Log.Debug($"[TargetableTrue_LittleLadies] TargetNpc found! DataId: {targetNpc.DataId}, ObjectId: {targetNpc.GameObjectId}, Name: {targetNpc.Name}");
                accessory.Method.UseAction((uint)targetNpc.GameObjectId, myActionId);

                
            }
            else
            {
                // 我要声援的小偶像不可选中
                accessory.Log.Debug($"[TargetableTrue_LittleLadies] TargetNpc NOT found! DataId: {npcDataId}");
                // 根据SourceId来确定要使用哪个技能
                IGameObject? sourceNpc = accessory.Data.Objects.SearchById(sourecId);
                if (sourceNpc is not null)
                {
                    myActionId = sourceNpc.DataId switch
                    {
                        18859 => 44501, // 乌拉拉小红
                        18860 => 44502, // 鸣海小黄
                        18861 => 44503, // 马夏·玛卡拉卡小蓝
                        18862 => 44504, // 皮科特小紫
                        _ => myActionId, // 默认不变
                    };
                    // 选中一下目标，让你看起来没那么像挂
                    accessory.Method.SelectTarget((uint)sourceNpc.GameObjectId);

                    
                    accessory.Log.Debug($"[TargetableTrue_LittleLadies] SourceNpc found! DataId: {sourceNpc.DataId}, ObjectId: {sourceNpc.GameObjectId}, Name: {sourceNpc.Name}");
                    accessory.Method.UseAction((uint)sourceNpc.GameObjectId, myActionId);
                    
                }
            }
        }
        

        // TODO, 额，续一下公演观众状态？ 怎么做？
        // 方案1 ，使用vnavmesh寻路，然后怎么和NPC交互呢？
        // 方案2 ，直接语音提示，人工续状态。（好，就选这个！）


        DateTime lastStatusAddTime = DateTime.MinValue;

        [ScriptMethod(name: "Little Ladies 公演观众状态赋予",
            eventType: EventTypeEnum.StatusAdd,
            suppress : 1000,
            eventCondition: ["StatusID:regex:^(1494)$", "SourceId:E0000000"])]
        public void StatusAdd_Fans(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me) return;
            accessory.Log.Debug($"[StatusAdd_Fans] My Fan status Add!");
            // 如何区分手动移除和自动移除呢？ 添加一个检测时间戳
            lastStatusAddTime = DateTime.Now;
        }

        [ScriptMethod(name: "Little Ladies 公演状态状态移除",
            eventType: EventTypeEnum.StatusRemove,
            suppress : 1000,
            eventCondition: ["StatusID:regex:^(1494)$", "SourceId:E0000000"])]
        public void StatusRemove_Fans(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me) return;
            accessory.Log.Debug($"[StatusRemove_Fans] My Fan status removed!");
            // 如何区分手动移除和自动移除呢？ 添加一个检测时间戳
            // 如果和上一个状态添加的时间差大于1700秒，就认为是自动移除，需要提示下
            if (DateTime.Now - lastStatusAddTime > TimeSpan.FromSeconds(1700) && FanStatusReminder)
            {
                accessory.Log.Debug($"[StatusRemove_Fans] Status removed by timeout, giving reminder.");
                // 语音提示，人工续状态
                if (FanStatusReminderVoice)
                {
                    accessory.Method.TTS("续一下公演状态哦！");
                }
                accessory.Method.SendChat("/e <se.1>");
            }
        }
    }
}
