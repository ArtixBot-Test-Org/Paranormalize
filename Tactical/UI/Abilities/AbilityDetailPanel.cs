using Godot;

namespace UI;

public partial class AbilityDetailPanel : Control
{
	private AbstractAbility _ability;
	public AbstractAbility Ability {
		get {return _ability;}
		set {_ability = value; UpdateDescriptions();}
	}

	private Label _abilityDesc;
	public string AbilityDesc {
		get {return _abilityDesc?.Text;}
		set {_abilityDesc.Text = value;}
	}
	
	private readonly PackedScene abilityDie = GD.Load<PackedScene>("res://Tactical/UI/Abilities/AbilityDie.tscn");

	private Label abilityType;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready(){
		_abilityDesc = GetNode<Label>("Ability Desc");
		abilityType = GetNode<Label>("Ability Type");
	}

	private void UpdateDescriptions(){
		abilityType.Text = $"{_ability.TYPE} | Range: {_ability.MIN_RANGE} - {_ability.MAX_RANGE} | Cooldown: {_ability.BASE_CD}";
		if (_ability.STRINGS.ContainsKey("GENERIC")){
			_abilityDesc.Text = _ability.STRINGS["GENERIC"];
		} else {
			_abilityDesc.Text = "";
		}

		for (int i = 0; i < _ability.BASE_DICE.Count; i++){
            AbilityDie node = (AbilityDie) abilityDie.Instantiate();
			AddChild(node);
			node.SetPosition(new Vector2(10, 50 + (i * 50)));		// TODO: Reevaluate use of constant values.

			node.Die = _ability.BASE_DICE[i];
            if (_ability.STRINGS.ContainsKey(node.Die.DieId)){
				node.DieDesc = _ability.STRINGS[node.Die.DieId];
			}

		}
	}
}