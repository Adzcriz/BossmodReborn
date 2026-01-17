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

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes.AsSpan();

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.WyvernsWeal2)
            _aoes.Add(new AOEInstance(_shape, caster.Position, caster.Rotation, WorldState.FutureTime(0.8f)));
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation <= now);
    }
}

// Wyvern's Weal: simple, deterministic "GTFO lane" using boss cast 45046/45047.
// Draws an asymmetric lane that extends both forward and backward during the cast.
// Warning is best-effort: we nudge AI early and show the lane as soon as the cast starts.
sealed class WyvernsWealIrregularCastLane(BossModule module) : Components.GenericAOEs(module)
{
    private const float LenFront = 60f;
    private const float LenBack = 20f;   // behind the boss
    private const float Narrow = 1f;
    private const float Wide = 60f;
    private const float PullBack = 40f;  // tuned: shifts lane toward arena boundary

    private AOEShapeRect? _shape;
    private ShapeDistance? _forbid;
    private WPos _sdOrigin;   // reference origin for forward/back lengths (must match forbid)
    private Angle _rot;
    private DateTime _until;

    // cached for the optional early-goal scoring; not required for rendering
    private float _halfW;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_shape == null || WorldState.CurrentTime >= _until)
            return default;

        var f = _rot.ToDirection();
        var center = _sdOrigin + f * ((LenFront - LenBack) * 0.5f);
        return new[] { new AOEInstance(_shape, center, _rot, _until) };
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;

        // 45047 = north/right sweep -> danger on RIGHT (safe LEFT)
        // 45046 = southeast/left sweep -> danger on LEFT (safe RIGHT)
        if (spell.Action.ID is not (45046u or 45047u))
            return;

        bool dangerOnRight = spell.Action.ID == 45047u;

        _rot = spell.Rotation;
        var f = _rot.ToDirection();
        var left = new WDir(-f.Z, f.X);

        // asym lateral extents: allow s in [-lw .. +rw]
        var lw = dangerOnRight ? Narrow : Wide;
        var rw = dangerOnRight ? Wide : Narrow;

        _halfW = (lw + rw) * 0.5f;
        var lateralShift = (lw - rw) * 0.5f;

        // SDRect origin is the point where forward/back lengths are measured
        _sdOrigin = caster.Position + lateralShift * left;

        // pull the whole lane back so it starts closer to the boundary rather than from boss hitbox center
        _sdOrigin -= f * PullBack;

        _shape = new AOEShapeRect(LenFront + LenBack, _halfW);
        _forbid = new SDRect(_sdOrigin, f, LenFront, LenBack, _halfW);

        _until = Module.CastFinishAt(spell); // ~7s
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (45046u or 45047u))
        {
            _shape = null;
            _forbid = null;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_forbid == null || WorldState.CurrentTime >= _until)
            return;

        // Hard forbid for the full cast duration: AI will GTFO immediately
        hints.AddForbiddenZone(_forbid, _until);

        // Optional: add a stronger "get out" bias early in the cast (first ~1.5s)
        var remaining = _until - WorldState.CurrentTime;
        if (remaining > TimeSpan.FromSeconds(5.5))
        {
            var forbid = _forbid;
            hints.GoalZones.Add(pos => forbid.Contains(pos) ? -200f : 10f);
        }
    }
}
