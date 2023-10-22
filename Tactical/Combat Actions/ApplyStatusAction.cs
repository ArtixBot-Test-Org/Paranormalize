using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ApplyStatusAction : AbstractAction {

    private AbstractCharacter target;
    private AbstractStatusEffect effect;
    private int stacksToApply;

    public ApplyStatusAction(AbstractCharacter target, AbstractStatusEffect effect, int stacksToApply = 0){
        this.target = target;
        this.effect = effect;
        this.stacksToApply = stacksToApply;
    }

    public override void Execute(){
        if (this.target == null) return;
        AbstractStatusEffect existingEffect = this.target.statusEffects.FirstOrDefault(status => status.ID.Equals(this.effect.ID));

        // If the effect already exists, add stacks to it if applicable.
        if (existingEffect != null){
            if (this.stacksToApply > 0 && existingEffect.STACKS > 0){
                existingEffect.STACKS += this.stacksToApply;
            }
            return;
        }

        this.target.statusEffects.Add(effect);
        effect.InitSubscriptions();
        // TODO: CombatManager.eventManager.BroadcastEvent(new CombatEventEffectApplied).
        return;
    }
}