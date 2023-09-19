using Godot;
using System;
using System.Data;

public partial class InterfaceLayer : Control, IEventSubscriber {

	private Label roundCounter;
	private Label turnList;
	private Label abilityList;
	private Button endRoundButton;		// TODO: Remove this. There should not be an option to end the round as that's automatically calculated.

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		CombatManager.eventManager?.Subscribe(CombatEventType.ON_ROUND_START, this, CombatEventPriority.LOWEST_PRIORITY);
		CombatManager.eventManager?.Subscribe(CombatEventType.ON_TURN_START, this, CombatEventPriority.LOWEST_PRIORITY);
	}

	public void _on_round_counter_ready(){
		roundCounter = GetNode<Label>("RoundCounter");
		UpdateRoundCounter();
	}

	public void _on_turn_list_ready(){
		turnList = GetNode<Label>("Turnlist");
		UpdateTurnlistText();
	}

	public void _on_button_ready(){
		endRoundButton = GetNode<Button>("Button");
		endRoundButton.Pressed += EndRound;
	}

	public void _on_ability_list_ready(){
		abilityList = GetNode<Label>("AbilityList");
		UpdateAbilityListText();
	}

	private void UpdateRoundCounter(){
		CombatInstance combatInstance = CombatManager.combatInstance;
		if (combatInstance != null) {
			roundCounter.Text = $"Round {combatInstance.round}";
		}
	}

	private void UpdateTurnlistText(){
		CombatInstance combatInstance = CombatManager.combatInstance;
		if (combatInstance == null) return;
		string turnlistText = $"Remaining turns: {combatInstance.activeChar.CHAR_NAME} ({combatInstance.activeCharSpd}), ";
		foreach ((CharacterInfo info, int spd) in combatInstance.turnlist.GetQueue()){
			turnlistText += $"{info.CHAR_NAME} ({spd}), ";
		}
		turnList.Text = turnlistText;
	}

	private void UpdateAbilityListText(){
		CombatInstance combatInstance = CombatManager.combatInstance;
		if (combatInstance == null) return;
		string abilityListText = "ABILITIES\n=======\n";
		foreach (AbstractAbility ability in combatInstance.activeChar.abilities){
			abilityListText += $"{ability.NAME} (Cooldown: {ability.BASE_CD})\n";
		}
		abilityList.Text = abilityListText;
	}


	private void EndRound(){
		CombatManager cm = GetNode<CombatManager>("/root/TacticalScene/CombatManager");
		cm?.ChangeCombatState(CombatState.TURN_END);
	}
	
    public void HandleEvent(CombatEventData data){
		switch (data.eventType){
			case CombatEventType.ON_ROUND_START:
				UpdateRoundCounter();
				break;
			case CombatEventType.ON_TURN_START:
				UpdateTurnlistText();
				UpdateAbilityListText();
				break;
		}
	}
}