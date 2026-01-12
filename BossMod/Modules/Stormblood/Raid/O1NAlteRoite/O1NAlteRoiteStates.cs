namespace BossMod.Stormblood.Raid.O1NAlteRoite;
sealed class O1NAlteRoiteStates : StateMachineBuilder
{
    public O1NAlteRoiteStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Burn>()
            .ActivateOnEnter<Roar>()
            .ActivateOnEnter<Blaze>()
            .ActivateOnEnter<BreathWing>()
            .ActivateOnEnter<ThinIce>()
            .ActivateOnEnter<WyrmTail>()
            .ActivateOnEnter<TwinBolt>()
            .ActivateOnEnter<Levinbolt>()
            .ActivateOnEnter<Clamp>()
            .ActivateOnEnter<Flame>()
            .ActivateOnEnter<Charybdis>()
            .ActivateOnEnter<Levinbolt1>();
    }
}
