using CharacterPassives;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UI;

public partial class CharacterUI : Area2D, IEventSubscriber, IEventHandler<CombatEventTurnEnd>, IEventHandler<CombatEventCombatStateChanged>
{
	[Signal]
	public delegate void CharacterSelectedEventHandler(CharacterUI character);

	private readonly Dictionary<StatusEffectType, string> statusToColorMap = new(){
        {StatusEffectType.BUFF, "#4cf"},
        {StatusEffectType.CONDITION, "#ffae00"},
        {StatusEffectType.DEBUFF, "#f74040"}
    };

	private AbstractCharacter _character;
	public AbstractCharacter Character {
		get {return _character;}
		set {_character = value; UpdateSprite();}
	}

	public Dictionary<string, Texture2D> Poses = new();
	public Sprite2D Sprite;
	private Label HPStat;
	private RichTextLabel PoiseStat;
	
	private TextureRect activeBuffs;
	private TextureRect activeConditions;
	private TextureRect activeDebuffs;
	private TextureRect passives;

	private readonly Material selectableMaterial = GD.Load<Material>("res://Tactical/UI/Shaders/CharacterTargetable.tres");
	private readonly PackedScene tooltip = GD.Load<PackedScene>("res://Tactical/UI/Components/Tooltip.tscn");

	private static double tooltipFadeDuration = 0.15;
	private List<Tooltip> hoverTooltips = new();

	private bool _IsClickable;
	public bool IsClickable {
		get {return _IsClickable;}
		set {
			_IsClickable = value; 
			this.InputPickable = IsClickable;				// LINK - Tactical\UI\GUIOrchestrator.cs:59
			Sprite.Material = IsClickable ? selectableMaterial : null;
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		Sprite = GetNode<Sprite2D>("Sprite2D");
		HPStat = GetNode<Label>("Sprite2D/HP/Label");
		PoiseStat = GetNode<RichTextLabel>("Sprite2D/Poise/Label");

		activeBuffs = GetNode<TextureRect>("Sprite2D/Active Buffs");
		activeConditions = GetNode<TextureRect>("Sprite2D/Active Conditions");
		activeDebuffs = GetNode<TextureRect>("Sprite2D/Active Debuffs");
		passives = GetNode<TextureRect>("Sprite2D/Passives");

		IsClickable = false;

		UpdateSprite();
		UpdatePassives();	// A character can't gain/lose passives in the middle of combat, so this can be done in ready.
		InitSubscriptions();
	}

    public void _on_input_event(Viewport viewport, InputEvent @event, int shape_idx){
        if (IsClickable && @event is InputEventMouseButton && @event.IsPressed() == false){
			EmitSignal(nameof(CharacterSelected), this);
		}
    }

	public void _on_mouse_entered(){
		ActiveCharInterfaceLayer activeCharNode = GetNode<ActiveCharInterfaceLayer>("../HUD/GUI/Active Character");
		// TODO: Handle case when the unit is staggered (see second condition) more artfully.
		if (IsClickable && CombatManager.combatInstance.turnlist.ContainsItem(_character) && IsInstanceValid(activeCharNode) && _character.Behavior?.reactions.Count > 0){
			activeCharNode.CreateAbilityDetailPanel(_character.Behavior.reactions.First(), true);
		}
	}

	public void _on_mouse_exited(){
		ActiveCharInterfaceLayer activeCharNode = GetNode<ActiveCharInterfaceLayer>("../HUD/GUI/Active Character");
		if (IsInstanceValid(activeCharNode)){
			activeCharNode.DeleteAbilityDetailPanel(true);
		}
	}

	public void _on_tree_exited(){
		CombatManager.eventManager?.UnsubscribeAll(this);
	}

	public async void TooltipFade(Control node){
		float currentTime = 0f;
		Godot.Vector2 startPos = node.Position;
		Godot.Vector2 endPos = node.Position + new Godot.Vector2(20, 0);

		// Use vectors so we can use Godot's in-built Lerp function.
		Godot.Vector2 startBlurRadius = new Godot.Vector2(0.0f, 0.0f);
		Godot.Vector2 endBlurRadius = new Godot.Vector2(1.0f, 0.0f);

		while (currentTime <= tooltipFadeDuration){
			if (!IsInstanceValid(node)) { return; }
            float normalized = Math.Min((float)(currentTime / tooltipFadeDuration), 1.0f);
			node.Position = startPos.Lerp(endPos, Lerpables.EaseOut(normalized, 5));
			node.Modulate = new Color(node.Modulate.R, node.Modulate.G, node.Modulate.B, startBlurRadius.Lerp(endBlurRadius, Lerpables.EaseOut(normalized, 5)).X);

			await Task.Delay(1);
            currentTime += (float)GetProcessDeltaTime();		// Not using PhysicsProcess since this is graphical effect only.
        }
	}

	public  void _on_active_buffs_mouse_entered(){
		var buffs = this.Character.statusEffects.Where(effect => effect.TYPE == StatusEffectType.BUFF).ToList();
		int offsetY = 0;
		for (int i = 0; i < buffs.Count; i++){
			AbstractStatusEffect buff = buffs[i];

			string parsedName = ParseString(buff.NAME, buff);
            string parsedDesc = ParseString(buff.DESC, buff);
			string effectString = $"[color={statusToColorMap[buff.TYPE]}][b]{parsedName}[/b][/color]\n{parsedDesc}";

			Tooltip tooltipNode = (Tooltip) tooltip.Instantiate();
			AddChild(tooltipNode);
			tooltipNode.Strings = new List<string>{effectString};
			hoverTooltips.Add(tooltipNode);
			tooltipNode.SetPosition(new Godot.Vector2(100, offsetY));
			TooltipFade(tooltipNode);
			tooltipNode.AddThemeStyleboxOverride("panel", GD.Load<StyleBoxTexture>("res://Tactical/UI/Components/BuffGradientBG.tres"));

			// 10 padding + 16 for margins for tooltip
			offsetY += 26 + tooltipNode.rtlNode.GetContentHeight();
		}
	}

	public void _on_active_conditions_mouse_entered(){
		var conditions = this.Character.statusEffects.Where(effect => effect.TYPE == StatusEffectType.CONDITION).ToList();
		int offsetY = 0;
		for (int i = 0; i < conditions.Count; i++){
			AbstractStatusEffect condition = conditions[i];

			string parsedName = ParseString(condition.NAME, condition);
            string parsedDesc = ParseString(condition.DESC, condition);
			string effectString = $"[color={statusToColorMap[condition.TYPE]}][b]{parsedName}[/b][/color]\r\n{parsedDesc}";

			Tooltip tooltipNode = (Tooltip) tooltip.Instantiate();
			AddChild(tooltipNode);
			tooltipNode.Strings = new List<string>{effectString};
			hoverTooltips.Add(tooltipNode);
			tooltipNode.SetPosition(new Godot.Vector2(100, offsetY));
			TooltipFade(tooltipNode);
			tooltipNode.AddThemeStyleboxOverride("panel", GD.Load<StyleBoxTexture>("res://Tactical/UI/Components/ConditionGradientBG.tres"));

			// 10 padding + 16 for margins for tooltip
			offsetY += 26 + tooltipNode.rtlNode.GetContentHeight();
		}
	}

	public void _on_active_debuffs_mouse_entered(){
		var debuffs = this.Character.statusEffects.Where(effect => effect.TYPE == StatusEffectType.DEBUFF).ToList();
		int offsetY = 0;
		for (int i = 0; i < debuffs.Count; i++){
			AbstractStatusEffect debuff = debuffs[i];

			string parsedName = ParseString(debuff.NAME, debuff);
            string parsedDesc = ParseString(debuff.DESC, debuff);
			string effectString = $"[color={statusToColorMap[debuff.TYPE]}][b]{parsedName}[/b][/color]\n{parsedDesc}";

			Tooltip tooltipNode = (Tooltip) tooltip.Instantiate();
			AddChild(tooltipNode);
			hoverTooltips.Add(tooltipNode);

			tooltipNode.Strings = new List<string>{effectString};
			tooltipNode.SetPosition(new Godot.Vector2(100, offsetY));
			tooltipNode.AddThemeStyleboxOverride("panel", GD.Load<StyleBoxTexture>("res://Tactical/UI/Components/DebuffGradientBG.tres"));
			TooltipFade(tooltipNode);

			// 10 padding + 16 for margins for tooltip
			offsetY += 26 + tooltipNode.rtlNode.GetContentHeight();
		}
	}

	public void _on_passives_mouse_entered(){
		var passives = this.Character.passives;
		int offsetY = 0;
		foreach (AbstractPassive passive in passives){
			string effectString = $"[b]{passive.NAME}[/b]\n{passive.DESC}";

			Tooltip tooltipNode = (Tooltip) tooltip.Instantiate();
			AddChild(tooltipNode);
			hoverTooltips.Add(tooltipNode);
			
			tooltipNode.SetPosition(new Godot.Vector2(-600, offsetY));
			tooltipNode.Strings = new List<string>{effectString};
			TooltipFade(tooltipNode);
			
			// 10 padding + 16 for margins for tooltip
			offsetY += 26 + tooltipNode.rtlNode.GetContentHeight();
		}
	}

	public void _on_active_buffs_mouse_exited(){
		foreach (Tooltip tooltip in hoverTooltips){
			tooltip.QueueFree();
		}
		hoverTooltips.Clear();
	}

	public void _on_active_conditions_mouse_exited(){
		foreach (Tooltip tooltip in hoverTooltips){
			tooltip.QueueFree();
		}
		hoverTooltips.Clear();
	}

	public void _on_active_debuffs_mouse_exited(){
		foreach (Tooltip tooltip in hoverTooltips){
			tooltip.QueueFree();
		}
		hoverTooltips.Clear();
	}

	public void _on_passives_mouse_exited(){
		foreach (Tooltip tooltip in hoverTooltips){
			tooltip.QueueFree();
		}
		hoverTooltips.Clear();
	}

	private void UpdateStatsText(){
		if (Character == null || HPStat == null || PoiseStat == null) {return;}
		HPStat.Text = $"{Character.CurHP}";
		PoiseStat.Text = $"[font n='res://Assets/Jost-Medium.ttf' s=24]{Character.CurPoise}[/font]";
		if (Character.CurPoise == 0) {
			PoiseStat.Text = "[shake]" + PoiseStat.Text + "[/shake]";
		}
	}

	private void UpdateSprite(){
		if (Character == null || Sprite == null) {return;}
		if (Character.CHAR_FACTION == CharacterFaction.PLAYER){
			Sprite.Texture = ResourceLoader.Load<Texture2D>("res://Sprites/Characters/Duelist/idle.png");
		}
		if (Character.CHAR_FACTION == CharacterFaction.ENEMY){
			Sprite.Texture = ResourceLoader.Load<Texture2D>("res://Sprites/Characters/Test Dummy/idle.png");
		}
		// TODO: Preload this during game instead of doing this here.
		using var dir = DirAccess.Open($"res://Sprites/Characters/{Character.CHAR_NAME}");
		if (dir != null){
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != ""){
				if (fileName.EndsWith("import")){
					fileName = dir.GetNext();
					continue;
				}
				Poses[fileName.TrimSuffix(".png")] = GD.Load<Texture2D>($"res://Sprites/Characters/{Character.CHAR_NAME}/{fileName}");
				fileName = dir.GetNext();
			}
		}
	}

	private void UpdateBuffs(){
		if (!Character.HasBuff) {
			activeBuffs.Visible = false;
			return;
		}
		activeBuffs.Visible = true;
		// TODO: Add on-hover functionality.
	}

	private void UpdateDebuffs(){
		if (!Character.HasDebuff) {
			activeDebuffs.Visible = false;
			return;
		}
		activeDebuffs.Visible = true;
		// TODO: Add on-hover functionality.
	}

	private void UpdateConditions(){
		if (Character.statusEffects.Where(effect => effect.TYPE == StatusEffectType.CONDITION).ToList().Count == 0) {
			activeConditions.Visible = false;
			return;
		}
		activeConditions.Visible = true;
		// TODO: Add on-hover functionality.
	}

	private void UpdatePassives(){
		if (Character.passives.Count == 0) {
			passives.Visible = false;
			return;
		}
		passives.Visible = true;
	}

	public virtual void InitSubscriptions(){
		// TODO: Change this to something like ON_TAKE_DAMAGE or ON_HP_CHANGED instead.
		CombatManager.eventManager?.Subscribe(CombatEventType.ON_TURN_END, this, CombatEventPriority.UI);
		CombatManager.eventManager?.Subscribe(CombatEventType.ON_COMBAT_STATE_CHANGE, this, CombatEventPriority.UI);
    }

    public void HandleEvent(CombatEventTurnEnd eventData){
        UpdateStatsText();
    }

	public void HandleEvent(CombatEventCombatStateChanged eventData){
        UpdateStatsText();
		UpdateBuffs();
		UpdateDebuffs();
		UpdateConditions();
    }

	string ParseString(string s, AbstractStatusEffect effect){
        MatchCollection matches = new Regex(@"(?<=\{)(.*?)(?=\})").Matches(s);
        string prefix = $"[color={statusToColorMap[effect.TYPE]}]";
        string suffix = "[/color]";
        for (int i = 0; i < matches.Count; i++){
            Match match = matches[i];
            
            if (match.Value.Contains("stacks")){
                s = s.Replace("{" + match.Value + "}", prefix + effect.STACKS + suffix);
            }
            else if (match.Value.Contains("owner")){
                s = s.Replace("{" + match.Value + "}", effect.OWNER.CHAR_NAME);
            } else {    // Check for custom field w/ reflection. E.g. staggered condition uses "UNSTAGGER_ROUND" in effects.json. Check for an equivalent in ConditionStaggered.
                string customValue = effect.GetType().GetField(match.Value).GetValue(effect).ToString();
                s = s.Replace("{" + match.Value + "}", customValue);
            }
        }
        return s;
    }
}
