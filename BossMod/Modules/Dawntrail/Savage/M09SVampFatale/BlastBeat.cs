namespace BossMod.Dawntrail.Savage.M09SVampFatale;

sealed class BlastBeat(BossModule module) : Components.GenericAOEs(module)
{
    // vamplette actors created at start of fight
    // bats have action timeline 0x11D1, rotations already assigned, doesn't change until after status gained
    // check for renderflag -> 0 or timeline for visibility
    // becomes visible 4.5s before they all gain a VampetteDistance status with extra
    // better to draw based on time since start of mech and status extra?
    // gains status at 29.997 (30s)
    // Vamp Stomp resolves 30.5s
    // close bats explode 34.63s x2
    // mid bats explode 38.20s x3
    // far bats explode 41.68 x5
    // 3.5s between bat explosions; circle starts growing at 31s?
    // nothing appears for growing circle, may have to do that manually

    class Vampette
    {
        public Actor Actor;
        public Angle Rotation;
        public DateTime Activation;
        public WPos EndPos;
    }

    private readonly List<Vampette> _vampettes = [];
    private readonly double _blastDelay = 3.5d;

    public bool IsActive => _vampettes.Count > 0;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_vampettes.Count == 0)
            return [];

        var vamps = CollectionsMarshal.AsSpan(_vampettes);
        var len = vamps.Length;
        List<AOEInstance> aoes = [];

        for (var i = 0; i < len; i++)
        {
            if (vamps[i].Activation == default)
                continue;

            if (vamps[i].Activation.AddSeconds(-1 * _blastDelay) <= WorldState.CurrentTime)
                aoes.Add(new(new AOEShapeCircle(8f), vamps[i].EndPos, activation: vamps[i].Activation));
        }

        return CollectionsMarshal.AsSpan([.. aoes]);
    }

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (actor.OID == (uint)OID.VampetteFatale)
        {
            if (id == 0x11D1)
            {
                _vampettes.Add(new() { Actor = actor, Rotation = actor.Rotation });
            }
        }
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.VampetteDistance)
        {
            var vamps = CollectionsMarshal.AsSpan(_vampettes);
            var len = vamps.Length;

            for (var i = 0; i < len; i++)
            {
                if (vamps[i].Actor.InstanceID == actor.InstanceID)
                {
                    var vamp = vamps[i];
                    var direction = actor.Rotation.ToDirection().OrthoL().Dot(actor.DirectionTo(Arena.Center)) > 0 ? 1 : -1;
                    var rotation = direction * 90.Degrees();
                    var vampdiff = actor.Position - Arena.Center;
                    var final = vampdiff.Rotate(rotation);

                    vamp.EndPos = Arena.Center + final;
                    vamp.Activation = status.Extra == 0x25 ? WorldState.FutureTime(4.6d) : status.Extra == 0x33 ? WorldState.FutureTime(8.1d) : WorldState.FutureTime(11.6d);
                }
            }

            _vampettes.Sort((a, b) => a.Activation < b.Activation ? 1 : -1);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.BlastBeatVampette)
        {
            var vamps = CollectionsMarshal.AsSpan(_vampettes);
            var len = vamps.Length;

            for (var i = 0; i < len; i++)
            {
                if (vamps[i].Actor.InstanceID == caster.InstanceID)
                {
                    NumCasts++;
                    _vampettes.RemoveAt(i);
                    return;
                }
            }
        }
    }
}

sealed class CurseOfTheBombpyre(BossModule module) : Components.GenericStackSpread(module)
{
    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.CurseOfTheBombpyre)
            Spreads.Add(new(actor, 8f));
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.CurseOfTheBombpyre)
        {
            var count = Spreads.Count;
            var spreads = CollectionsMarshal.AsSpan(Spreads);
            for (var i = 0; i < count; ++i)
            {
                if (spreads[i].Target.InstanceID == actor.InstanceID)
                {
                    Spreads.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
