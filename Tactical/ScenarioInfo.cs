using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// When initiating a combat encounter, the ScenarioInfo provides all of the information necessary to the CombatManager.
/// </summary>
public partial class ScenarioInfo {
    public List<(AbstractCharacter character, int startingPosition)> fighters;

    public ScenarioInfo(List<(AbstractCharacter character, int startingPosition)> fighters){
        this.fighters = fighters;
    }
}

public class TestScenario : ScenarioInfo {

    private static readonly AbstractCharacter playerA = new AbstractCharacter("Quill", CharacterFaction.PLAYER);
    private static readonly AbstractCharacter playerB = new AbstractCharacter("Cinq", CharacterFaction.PLAYER);
    private static readonly AbstractCharacter characterB = new AbstractCharacter("Test Dummy A", CharacterFaction.ENEMY);
    private static readonly AbstractCharacter characterC = new AbstractCharacter("Test Dummy B", CharacterFaction.ENEMY);
    private static readonly AbstractCharacter characterD = new AbstractCharacter("Test Dummy C", CharacterFaction.ENEMY);
    private static List<(AbstractCharacter character, int position)> scenarioFighters = new(){
        (playerA, 3),
        // (playerB, 2),
        (characterB, 4),
        (characterC, 5),
        (characterD, 6),
    };

    public TestScenario() : base(scenarioFighters){
        playerA.EquipAbility(new TestReact());
        playerA.EquipAbility(new RelentlessStabbing());
        playerA.EquipAbility(new TestAttack());
        playerA.EquipAbility(new Discharge());
        playerA.EquipAbility(new Retreat());
        playerA.EquipAbility(new BalestraFente());
        playerA.EquipAbility(new Thwack());
        playerA.MinSpd = 0;
        playerA.MaxSpd = 0;

        playerB.EquipAbility(new Discharge());
        playerB.EquipAbility(new Repartee());
        playerB.MinSpd = 10;
        playerB.MaxSpd = 30;

        characterB.MaxHP = 100;
        characterB.CurHP = 100;
        characterB.MaxPoise = 10;
        characterB.EquipAbility(new Thwack());
        characterB.EquipAbility(new Discharge());
        characterB.ActionsPerTurn = 1;
        characterB.Behavior = new AiBehaviorPureRandom(characterB);
        characterC.ActionsPerTurn = 0;
        characterD.ActionsPerTurn = 0;
    }
}