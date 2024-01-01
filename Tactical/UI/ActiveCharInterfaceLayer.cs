using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UI;

public partial class ActiveCharInterfaceLayer : Control, IEventSubscriber, IEventHandler<CombatEventTurnStart>, IEventHandler<CombatEventCombatStateChanged> {

	private Label charName;
	private Label userPromptText;

	private Control abilityListNode;
	private readonly PackedScene abilityDetailPanel = GD.Load<PackedScene>("res://Tactical/UI/Abilities/AbilityDetailPanel.tscn");
	private readonly PackedScene targetingDialog = GD.Load<PackedScene>("res://Tactical/UI/Targeting Panel/SelectTargetPanel.tscn");
	private readonly PackedScene abilityButton = GD.Load<PackedScene>("res://Tactical/UI/AbilityButton.tscn");

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		CombatManager.combatInstance = new CombatInstance(new TestScenario());
        CombatManager.ChangeCombatState(CombatState.COMBAT_START);        // TODO: Remove, this is for debugging
		
		abilityListNode = GetNode<Control>("Ability List");
		charName = GetNode<Label>("Portrait/Name");
		userPromptText = GetNode<Label>("Prompt Text");

		UpdatePromptText("");
		UpdateAvailableAbilities();
		UpdateCharacterName();
		InitSubscriptions();
	}
	
	private AbilityDetailPanel abilityDetailPanelInstance = new();
	private List<Node> abilityButtonInstances = new List<Node>();
	private List<(int lane, HashSet<AbstractCharacter> targetsInLane)> abilityTargeting;
	private void UpdateAvailableAbilities(){
		// Remove previous instances from code.
		foreach(Node instance in abilityButtonInstances){
			abilityListNode.RemoveChild(instance);
		}
		abilityButtonInstances.Clear();

		CombatInstance combatInstance = CombatManager.combatInstance;

		if (combatInstance == null) return;
		AbstractCharacter activeChar = combatInstance.activeChar;
		for(int i = 0; i < activeChar.abilities.Count; i++){
			Button instance = (Button) abilityButton.Instantiate();
			abilityButtonInstances.Add(instance);
			instance.SetPosition(new Vector2(0, -i * instance.Size.Y));

			AbstractAbility ability = activeChar.abilities[i];
			instance.Disabled = !ability.IsAvailable || (ability.TYPE == AbilityType.REACTION && CombatManager.combatInstance.combatState != CombatState.AWAITING_CLASH_INPUT);
			instance.Text = ability.NAME + ((ability.curCooldown > 0) ? $" ({ability.curCooldown})" : "");
			instance.MouseEntered += () => CreateAbilityDetailPanel(ability);
			instance.MouseExited += () => DeleteAbilityDetailPanel();
			instance.Pressed += () => GetTargeting(ability); 
			abilityListNode.AddChild(instance);
		}
	}

	private void CreateAbilityDetailPanel(AbstractAbility ability){
		AbilityDetailPanel node = (AbilityDetailPanel) abilityDetailPanel.Instantiate();
		AddChild(node);
		node.Ability = ability;
		node.SetPosition(new Vector2(300, 750));

		abilityDetailPanelInstance = node;
	}

	private void DeleteAbilityDetailPanel(){
		RemoveChild(abilityDetailPanelInstance);
	}

	private void GetTargeting(AbstractAbility ability){
		abilityTargeting = ability.GetValidTargets();

		List<AbstractCharacter> characters = new();
		List<int> lanes = new();
		if (!ability.useLaneTargeting){
			characters = new();
			foreach ((int _, HashSet<AbstractCharacter> targetsInLane) in abilityTargeting){
				foreach (AbstractCharacter target in targetsInLane){
					characters.Add(target);
				}
			}
			if (characters.Count == 0) {
				Logging.Log("No targets were in range!", Logging.LogLevel.ESSENTIAL);
				return;
			}
			
		} else {
			lanes = new();
			foreach ((int lane, HashSet<AbstractCharacter> _) in abilityTargeting){
				lanes.Add(lane);
			}
			if (lanes.Count == 0) {
				Logging.Log("No lanes were in range!", Logging.LogLevel.ESSENTIAL);
				return;
			}
		}

		string promptText = (!ability.useLaneTargeting) ? "Select a unit" : "Select a lane";
		UpdatePromptText(promptText);

		// open unit-selection dialog
		// TODO: This should be a part of the regular combat interface instead of a separate dialog, but this is for testing purposes.
		SelectTargetPanel instance = (SelectTargetPanel) targetingDialog.Instantiate();
		instance.SetPosition(new Vector2(500, 500));
		this.AddChild(instance);
		instance.ability = ability;
		instance.RequiresText = !ability.useLaneTargeting;
		if (characters.Count > 0){
			instance.Chars = characters;
		} else {
			instance.Lanes = lanes;
		}
	}

	private void UpdatePromptText(string textToUse){
		userPromptText.Text = textToUse;
	}

	private void UpdateCharacterName(){
		CombatInstance combatInstance = CombatManager.combatInstance;
		if (combatInstance == null) return;
		charName.Text = combatInstance.activeChar.CHAR_NAME;
	}
	
	public virtual void InitSubscriptions(){
		CombatManager.eventManager.Subscribe(CombatEventType.ON_TURN_START, this, CombatEventPriority.UI);
		CombatManager.eventManager.Subscribe(CombatEventType.ON_COMBAT_STATE_CHANGE, this, CombatEventPriority.UI);
    }

    public void HandleEvent(CombatEventTurnStart data){
		UpdateAvailableAbilities();
		UpdateCharacterName();
		UpdatePromptText("");
	}

	public void HandleEvent(CombatEventCombatStateChanged data){
		if (data.prevState == CombatState.AWAITING_ABILITY_INPUT && data.newState == CombatState.AWAITING_CLASH_INPUT){
			UpdateAvailableAbilities();
		}
	}
}
