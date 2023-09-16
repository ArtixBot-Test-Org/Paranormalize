using Godot;
using System;
using System.Collections.Generic;

public interface IEventSubscriber {
    public virtual void HandleEvent(CombatEventData eventData){
        GD.Print($"{this.GetType()} is currently handling {eventData.GetType()}");
    }
}

public enum CombatEventType {
    ON_COMBAT_START, ON_COMBAT_END,
    ON_ROUND_START, ON_ROUND_END,
    ON_TURN_START, ON_TURN_END,
    ON_ABILITY_ACTIVATED,
    ON_UNIT_DEATH, ON_UNIT_DAMAGED,
    ON_DIE_ROLLED, ON_PRE_HIT,
    ON_CLASH_ELIGIBLE, ON_CLASH, ON_CLASH_WIN, ON_CLASH_TIE, ON_CLASH_LOSS,
    ON_STATUS_APPLIED, ON_STATUS_EXPIRED,
}

// Higher numbers execute first.
public enum CombatEventPriority {
    HIGHEST_PRIORITY = 99999,       // e.g. "revive on death", "prevent death"
    PASSIVE_EFFECT = 400,           // When an action is taken, a character's passives (and the target's passives) take priority over buffs.
    BUFF = 300,                     // Buffs have priority over debuffs.
    DEBUFF = 250,                   // Debuffs go *after* buffs.
    LOWEST_PRIORITY = 1,
}

public partial class CombatEventManager : Node{
    public Dictionary<CombatEventType, ModdablePriorityQueue<IEventSubscriber>> events;

    public CombatEventManager() {
        this.events = new Dictionary<CombatEventType, ModdablePriorityQueue<IEventSubscriber>>();
        foreach (CombatEventType type in Enum.GetValues(typeof(CombatEventType))){
            events.Add(type, new ModdablePriorityQueue<IEventSubscriber>());
        }
    }

    public bool Subscribe(CombatEventType eventType, IEventSubscriber subscriber, CombatEventPriority priority){
        if (!events.ContainsKey(eventType)){
            events.Add(eventType, new ModdablePriorityQueue<IEventSubscriber>());
        }
        if (!events[eventType].ContainsItem(subscriber)){
            events[eventType].AddToQueue(subscriber, (int)priority);
            return true;
        }
        return false;
    }

    // This invocation allows for custom priority values.
    public bool Subscribe(CombatEventType eventType, IEventSubscriber subscriber, int priority){
        if (!events.ContainsKey(eventType)){
            events.Add(eventType, new ModdablePriorityQueue<IEventSubscriber>());
        }
        if (!events[eventType].ContainsItem(subscriber)){
            events[eventType].AddToQueue(subscriber, priority);
            return true;
        }
        return false;
    }

    // Notify all subscribers of a specified event. Subscribers will execute in order.
    public void BroadcastEvent(CombatEventData eventData){
        if (!events.ContainsKey(eventData.eventType)) return;
        GD.Print($"CombatEventManager broadcasting event {eventData.eventType} to {events[eventData.eventType].GetQueue().Count} subscribers.");
        foreach ((IEventSubscriber subscriber, int _) in events[eventData.eventType].GetQueue()){
            subscriber.HandleEvent(eventData);
        }
    }

    // Return true if the subscriber is in the events dictionary for that event type, false otherwise.
    public bool CheckIfSubscribed(CombatEventType eventType, IEventSubscriber subscriber){
        if (!events.ContainsKey(eventType)) {return false;}
        return events[eventType].ContainsItem(subscriber);
    }

    // Remove a subscriber from a specified event type from the event dictionary.
    public void Unsubscribe(CombatEventType eventType, IEventSubscriber subscriber){
        if (!events.ContainsKey(eventType)){ return; }
        events[eventType].RemoveAllInstancesOfItem(subscriber);
    }

    // Remove the provided subscriber from all events. Used in instances such as when a character is defeated.
    public void UnsubscribeAll(IEventSubscriber subscriber){
        foreach (CombatEventType eventType in events.Keys){
            events[eventType].RemoveAllInstancesOfItem(subscriber);
        }
    }

	public override void _Ready(){
		GD.Print("CombatEventManager node loaded.");
	}
}

public abstract class CombatEventData {
    public CombatEventType eventType;
}

public class CombatEventCombatStart : CombatEventData {
    public CombatEventCombatStart(){
        this.eventType = CombatEventType.ON_COMBAT_START;
    }
}

public class CombatEventCombatEnd : CombatEventData {
    public CombatEventCombatEnd(){
        this.eventType = CombatEventType.ON_COMBAT_END;
    }
}

public class CombatEventRoundStart : CombatEventData {
    public int roundStartNum;

    public CombatEventRoundStart(int roundStartNum){
        this.eventType = CombatEventType.ON_ROUND_START;
        this.roundStartNum = roundStartNum;
    }
}

public class CombatEventRoundEnd : CombatEventData {
    public int roundEndNum;

    public CombatEventRoundEnd(int roundEndNum){
        this.eventType = CombatEventType.ON_ROUND_START;
        this.roundEndNum = roundEndNum;
    }
}