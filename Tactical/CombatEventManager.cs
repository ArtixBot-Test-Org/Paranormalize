using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public interface IEventHandler<T> where T : ICombatEvent {
    public abstract void HandleEvent(T eventData);
}

public interface IEventSubscriber{
    /// <summary>
    /// An IEventSubscriber should override InitSubscriptions to begin subscribing to all relevant events.
    /// Note that unsubscribing can be done with CombatEventManager.instance.UnsubscribeAll(this).
    /// </summary>
    public abstract void InitSubscriptions();
}

public enum CombatEventType {
    ON_COMBAT_STATE_CHANGE,
    ON_COMBAT_START, ON_COMBAT_END,
    ON_ROUND_START, ON_ROUND_END,
    ON_TURN_START, ON_TURN_END,
    ON_ABILITY_ACTIVATED,
    // Attacks will always trigger ON_DEAL_DAMAGE, then ON_TAKE_DAMAGE. Status effects like burn/bleed will only trigger ON_TAKE_DAMAGE.
    ON_DEAL_DAMAGE, ON_TAKE_DAMAGE,
    ON_CHARACTER_DEATH,
    ON_DIE_ROLLED, ON_DIE_HIT,
    ON_CLASH_ELIGIBLE, ON_CLASH, ON_CLASH_TIE, ON_CLASH_WIN, ON_CLASH_LOSE,
    ON_UNIT_MOVED,
    ON_STATUS_APPLIED, ON_STATUS_EXPIRED,
}

// Higher numbers execute first.
public enum CombatEventPriority {
    IMMEDIATELY = 99999,            // E.g. after an ability is activated, *immediately* set it on cooldown.
    DICE_MODIFICATION = 700,        // Adding/removing dice from a dice queue occur first.
    DICE_CONVERSION = 600,          // After all dice queue modifications finish, perform dice conversions (e.g. converting Evade -> Attack die, or Attack -> Block).
    BASE_ADDITIVE = 500,            // Additive modifiers to a value. These happen first and therefore have an outsized effect on multipliers.
    BASE_MULTIPLICATIVE = 400,      // Multiplicative multipliers to a value.
    STANDARD = 200,
    POST_DAMAGE_CALC = 100,         // E.g. passives which would prevent damage that would cause stagger or death should happen after all other calculations are complete.
    UI = 1,                         // UI updates should only update after everything else is done.
}

public partial class CombatEventManager{
    // When loading a new combat scenario, a new CombatEventManager instance is created during CombatInstance's constructor, and the instance is assigned to this.
    public static CombatEventManager instance;
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

    /// <summary>
    /// Notify all subscribers of a specified event. Subscribers will execute in order. Returns the modified eventData after all subscribers have finished eventData payload changes.
    /// </summary>
    public T BroadcastEvent<T>(T eventData) where T : ICombatEvent{
        if (!events.ContainsKey(eventData.eventType)) return default;
        Logging.Log($"CombatEventManager broadcasting event {eventData.eventType} to {events[eventData.eventType].GetQueue().Count} subscribers.", Logging.LogLevel.DEBUG);
        int i = 0;
        List<(IEventSubscriber subscriber, int priority)> eventSubscribers = events[eventData.eventType].GetQueue();
        while (i < eventSubscribers.Count){
            Logging.Log($"Handling subscriber #{i}. This one is {eventSubscribers[i].subscriber}.", Logging.LogLevel.DEBUG);
            (eventSubscribers[i].subscriber as IEventHandler<T>)?.HandleEvent(eventData);
            i += 1;
        }
        events[eventData.eventType].RemoveAllInstancesOfItem(null);
        return eventData;
    }

    // Return true if the subscriber is in the events dictionary for that event type, false otherwise.
    public bool CheckIfSubscribed(CombatEventType eventType, IEventSubscriber subscriber){
        if (!events.ContainsKey(eventType)) {return false;}
        return events[eventType].ContainsItem(subscriber);
    }

    // Remove a subscriber from a specified event type from the event dictionary.
    public void Unsubscribe(CombatEventType eventType, IEventSubscriber subscriber){
        if (!events.ContainsKey(eventType)){ return; }
        events[eventType].RemoveAllInstancesOfItem(subscriber, insteadMarkAsNull: true);
    }

    // Remove the provided subscriber from all events. Used in instances such as when a character is defeated.
    public void UnsubscribeAll(IEventSubscriber subscriber){
        foreach (CombatEventType eventType in events.Keys){
            events[eventType].RemoveAllInstancesOfItem(subscriber, insteadMarkAsNull: true);
        }
    }
}

public interface ICombatEvent {
    CombatEventType eventType {get;}
}

public class CombatEventCombatStart : ICombatEvent {

    public CombatEventType eventType {
        get {return CombatEventType.ON_COMBAT_START;}
    }
    
    public CombatEventCombatStart(){
    }
}

public class CombatEventCombatEnd : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_COMBAT_END;}
    }
    public bool playerWon;

    public CombatEventCombatEnd(bool playerWon){
        this.playerWon = playerWon;
    }
}

public class CombatEventRoundStart : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_ROUND_START;}
    }
    public int roundStartNum;

    public CombatEventRoundStart(int roundStartNum){
        this.roundStartNum = roundStartNum;
    }
}

public class CombatEventRoundEnd : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_ROUND_END;}
    }
    public int roundEndNum;

    public CombatEventRoundEnd(int roundEndNum){
        this.roundEndNum = roundEndNum;
    }
}

public class CombatEventTurnStart : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_TURN_START;}
    }
    public AbstractCharacter character;
    public int spd;

    public CombatEventTurnStart(AbstractCharacter character, int spd){
        this.character = character;
        this.spd = spd;
    }
}

public class CombatEventCombatStateChanged : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_COMBAT_STATE_CHANGE;}
    }
    public CombatState prevState;
    public CombatState newState;

    /// <summary>
    /// Mostly used for interfaces, specifically when changing from AWAITING_ABILITY_INPUT to AWAITING_CLASH_INPUT (so that we can enable Reaction button clicks)
    /// </summary>
    /// <param name="prevState"></param>
    /// <param name="newState"></param>
    public CombatEventCombatStateChanged(CombatState prevState, CombatState newState){
        this.prevState = prevState;
        this.newState = newState;
    }
}

public class CombatEventTurnEnd : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_TURN_END;}
    }
    public AbstractCharacter character;
    public int spd;

    public CombatEventTurnEnd(AbstractCharacter character, int spd){
        this.character = character;
        this.spd = spd;
    }
}

public class CombatEventCharacterDeath : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CHARACTER_DEATH;}
    }
    public AbstractCharacter deadChar;
    public CombatEventCharacterDeath(AbstractCharacter deadChar){
        this.deadChar = deadChar;
    }
}

public class CombatEventAbilityActivated : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_ABILITY_ACTIVATED;}
    }
    public AbstractAbility abilityActivated;
    public List<Die> abilityDice;
    public AbstractCharacter caster;
    public AbstractCharacter target;
    public List<AbstractCharacter> targets = new List<AbstractCharacter>();
    public List<int> lanes = new List<int>();

    public CombatEventAbilityActivated(AbstractCharacter caster, AbstractAbility abilityActivated, List<Die> abilityDice, AbstractCharacter target){
        this.abilityActivated = abilityActivated;
        this.abilityDice = abilityDice;
        this.caster = caster;
        this.target = target;
    }
    
    public CombatEventAbilityActivated(AbstractCharacter caster, AbstractAbility abilityActivated, List<Die> abilityDice, List<AbstractCharacter> targets){
        this.abilityActivated = abilityActivated;
        this.abilityDice = abilityDice;
        this.caster = caster;
        this.targets = targets;
        if (this.targets.Count == 1){
            this.target = targets.First();
        }
    }

    public CombatEventAbilityActivated(AbstractCharacter caster, AbstractAbility abilityActivated, List<Die> abilityDice, List<int> lanes){
        this.abilityActivated = abilityActivated;
        this.abilityDice = abilityDice;
        this.caster = caster;
        this.lanes = lanes;
    }
}

public class CombatEventClashOccurs : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CLASH;}
    }
    public AbstractCharacter attacker;
    public AbstractAbility attackerAbility;
    public List<Die> attackerDice;

    public AbstractCharacter reacter;
    public AbstractAbility reacterAbility;
    public List<Die> reacterDice;

    ///<summary>Notify subscribers that a clash is occurring.</summary>
    public CombatEventClashOccurs(AbstractCharacter attacker, AbstractAbility attackerAbility, List<Die> attackerDice, AbstractCharacter reacter, AbstractAbility reacterAbility, List<Die> reacterDice){
        this.attacker = attacker;
        this.attackerAbility = attackerAbility;
        this.attackerDice = attackerDice;

        this.reacter = reacter;
        this.reacterAbility = reacterAbility;
        this.reacterDice = reacterDice;
    }
}

public class CombatEventDieRolled : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_DIE_ROLLED;}
    }
    public Die die;
    public int rolledValue;

    public CombatEventDieRolled(Die die, int rolledValue){
        this.die = die;
        this.rolledValue = rolledValue;
    }
}

public class CombatEventDieHit : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_DIE_HIT;}
    }
    public AbstractCharacter hitter;
    public AbstractCharacter hitUnit;
    public Die die;
    public int naturalRoll;
    public bool rolledMinimumNaturalValue;
    public bool rolledMaximumNaturalValue;
    public int actualRoll;

    public CombatEventDieHit(AbstractCharacter hitter, AbstractCharacter hitUnit, Die die, int naturalRoll, int actualRoll){
        this.hitter = hitter;
        this.hitUnit = hitUnit;
        this.die = die;
        this.naturalRoll = naturalRoll;
        this.actualRoll = actualRoll;

        this.rolledMaximumNaturalValue = naturalRoll == die.MaxValue;
        this.rolledMinimumNaturalValue = naturalRoll == die.MinValue;
    }
}

public class CombatEventStatusApplied : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_STATUS_APPLIED;}
    }
    public AbstractStatusEffect statusEffect;
    public AbstractCharacter effectAppliedToChar;
    public int effectAppliedToLane;

    public CombatEventStatusApplied(AbstractStatusEffect effect, AbstractCharacter character){
        this.statusEffect = effect;
        this.effectAppliedToChar = character;
    }

    public CombatEventStatusApplied(AbstractStatusEffect effect, int lane){
        this.statusEffect = effect;
        this.effectAppliedToLane = lane;
    }
}

public class CombatEventDamageDealt : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_DEAL_DAMAGE;}
    }
    public AbstractCharacter dealer;
    public float damageDealt;
    public bool isPoiseDamage;

    public CombatEventDamageDealt(AbstractCharacter dealer, float damageDealt, bool isPoiseDamage){
        this.dealer = dealer;
        this.damageDealt = damageDealt;
        this.isPoiseDamage = isPoiseDamage;
    }
}

public class CombatEventDamageTaken : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_TAKE_DAMAGE;}
    }
    public AbstractCharacter target;
    public float damageTaken;
    public bool isPoiseDamage;

    public CombatEventDamageTaken(AbstractCharacter target, float damageTaken, bool isPoiseDamage){
        this.target = target;
        this.damageTaken = damageTaken;
        this.isPoiseDamage = isPoiseDamage;
    }
}

public class CombatEventClashTie : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CLASH_TIE;}
    }

    public Die atkDie;
    public Die reactDie;

    public CombatEventClashTie(Die atkDie, Die reactDie){
        this.atkDie = atkDie;
        this.reactDie = reactDie;
    }
}

public class CombatEventClashWin : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CLASH_WIN;}
    }
    public Die winningDie;
    public int winningRoll;

    public CombatEventClashWin(Die winningDie, int winningRoll){
        this.winningDie = winningDie;
        this.winningRoll = winningRoll;
    }
}

public class CombatEventClashLose : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CLASH_LOSE;}
    }
    public Die losingDie;
    public int losingRoll;

    public CombatEventClashLose(Die losingDie, int losingRoll){
        this.losingDie = losingDie;
        this.losingRoll = losingRoll;
    }
}


public class CombatEventUnitMoved : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_UNIT_MOVED;}
    }
    public AbstractCharacter movedUnit;
    public int originalLane;
    public int moveMagnitude;
    public bool isMoveLeft;       // If false, assume move right.
    public bool isForcedMovement;   // Voluntary lane shifts and shifts as part of an ability effect are not considered "forced movement", but Pushes/Pulls are.

    public CombatEventUnitMoved(AbstractCharacter movedUnit, int originalLane, int moveMagnitude, bool isMoveLeft, bool isForcedMovement){
        this.movedUnit = movedUnit;
        this.originalLane = originalLane;
        this.moveMagnitude = moveMagnitude;
        this.isMoveLeft = isMoveLeft;
        this.isForcedMovement = isForcedMovement;
    }
}

/// <summary>
/// Only UI elements should listen to this event. Triggers when an enemy attacks a player character who has eligible reaction abilities.
/// </summary>
public class CombatEventClashEligible : ICombatEvent {
    public CombatEventType eventType {
        get {return CombatEventType.ON_CLASH_ELIGIBLE;}
    }

    public AbstractCharacter attacker;
    public AbstractAbility attackerAbility;
    public AbstractCharacter defender;
    public List<AbstractAbility> reactableAbilities;

    public CombatEventClashEligible(AbstractCharacter attacker, AbstractAbility attackerAbility, AbstractCharacter defender, List<AbstractAbility> reactableAbilities){
        this.attacker = attacker;
        this.attackerAbility = attackerAbility;
        this.defender = defender;
        this.reactableAbilities = reactableAbilities;
    }
}