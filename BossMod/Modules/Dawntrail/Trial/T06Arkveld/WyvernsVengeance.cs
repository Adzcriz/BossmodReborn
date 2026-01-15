namespace BossMod.Dawntrail.Trial.T06Arkveld;

sealed class WyvernsVengeance(BossModule module) : Components.Exaflare(module, 6f)
{
    private readonly List<ulong> _casters = new();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.WyvernsVengeance)
        {
            // advance doesn't matter for caster-matched tracking; use default WDir
            Lines.Add(new(
                caster.Position,
                default,
                Module.CastFinishAt(spell),
                timeToMove: 1.6d,
                explosionsLeft: 2,
                maxShownExplosions: 1
            ));
            _casters.Add(caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.WyvernsVengeance1)
        {
            var idx = _casters.IndexOf(caster.InstanceID);
            if (idx < 0)
                return; // not one of our tracked helpers

            var line = Lines[idx];
            var loc = spell.TargetXZ;

            AdvanceLine(line, loc);

            if (line.ExplosionsLeft == 0)
            {
                Lines.RemoveAt(idx);
                _casters.RemoveAt(idx);
            }
        }
    }
}

// Don't think I am able to put the predicted path of the 'Laser' in, so this may have to do.
// Wyvern's Weal beam telegraphs (helpers)
sealed class WyvernsWealAOE(BossModule module)
    : Components.SimpleAOEGroups(module, [(uint)AID.WyvernsWeal1, (uint)AID.WyvernsWeal4], new AOEShapeRect(60f, 3f));

// Wyvern's Weal pulses (no-cast helper rects)
sealed class WyvernsWealPulses(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect _shape = new(60f, 3f);
    private readonly List<AOEInstance> _aoes = new();

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _aoes.AsSpan();

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.WyvernsWeal2)
        {
            // show pulse briefly
            var expiry = WorldState.FutureTime(5f);

            _aoes.Add(new AOEInstance(_shape, caster.Position, caster.Rotation, expiry));
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation <= now);
    }
}
