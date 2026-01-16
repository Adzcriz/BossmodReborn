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
            var expiry = WorldState.FutureTime(0.8f);
            _aoes.Add(new AOEInstance(_shape, caster.Position, caster.Rotation, expiry));
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation <= now);
    }
}

// Wyvern's Weal turn assist (simplified + stable):
// - Uses boss icon 501/502 to decide which side is "danger lane"
// - Renders an asymmetric lane on radar immediately (rect + shifted center; no polygons)
// - AI: during warning window prefers safe side (GoalZone)
// - AI: becomes forbidden immediately on boss cast 45046/45047
// 501 = turnright_c0g, 502 = turnleft_c0g
sealed class WyvernsWealTurnAssist(BossModule module) : Components.GenericAOEs(module)
{
    private const float Len = 60f;
    private const float Narrow = 1.5f;
    private const float Wide = 60f;

    private DateTime _activateAt;         // when we start forbidding (after warning) - or immediately on cast
    private DateTime _expireAt;           // when we stop showing/forbidding
    private AOEShapeRect? _aoeShape;      // radar rendering
    private ShapeDistance? _forbidShape;  // AI forbidden checks (SDRect)
    private Angle _rot;                  // wall-aligned-ish rotation
    private WPos _center;                // SHIFTED center for asym rect trick
    private uint _icon;                  // last seen 501/502

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_aoeShape == null)
            return default;

        var now = WorldState.CurrentTime;
        if (now >= _expireAt)
            return default;

        return new[] { new AOEInstance(_aoeShape, _center, _rot, _activateAt) };
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor != Module.PrimaryActor)
            return;

        if (iconID is 501 or 502)
        {
            _icon = iconID;

            // 5s warning before we hard-forbid (AI soft-moves during this time)
            _activateAt = WorldState.FutureTime(5.0);
            _expireAt = WorldState.FutureTime(9.0);

            BuildAsymLane(iconID, Module.PrimaryActor.Position);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // hard-forbid instantly when the boss starts the real Weal cast
        if (caster == Module.PrimaryActor && (spell.Action.ID == 45046u || spell.Action.ID == 45047u))
        {
            _activateAt = WorldState.CurrentTime;
            _expireAt = Module.CastFinishAt(spell);

            // if we didn't get the icon for some reason, infer from action id
            if (_aoeShape == null)
            {
                _icon = (spell.Action.ID == 45046u) ? 501u : 502u;
                BuildAsymLane(_icon, caster.Position);
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_forbidShape == null)
            return;

        var now = WorldState.CurrentTime;
        if (now >= _expireAt)
            return;

        // During warning window: soft-push toward safe side
        if (now < _activateAt)
        {
            var forbid = _forbidShape;
            hints.GoalZones.Add(pos =>
            {
                // strongly discourage being inside danger lane
                if (forbid.Contains(pos))
                    return -100f;
                // otherwise encourage being outside
                return 20f;
            });
            return;
        }

        // Once active: hard forbid the danger lane
        hints.AddForbiddenZone(_forbidShape, _expireAt);
    }

    private void BuildAsymLane(uint iconID, WPos origin)
    {
        // rotation should be arena-boundary aligned rather than boss facing
        _rot = WallAlignedRotation(origin, iconID);

        // icon->side mapping:
        // 501 => danger on RIGHT (safe LEFT)
        // 502 => danger on LEFT  (safe RIGHT)
        var lw = (iconID == 501) ? Narrow : Wide;
        var rw = (iconID == 501) ? Wide : Narrow;

        // Convert asym [-lw .. +rw] into symmetric rect + shifted center:
        // halfW = (lw + rw)/2
        // shift = (lw - rw)/2 along LEFT unit
        var halfW = (lw + rw) * 0.5f;
        var shift = (lw - rw) * 0.5f;

        var f = _rot.ToDirection();
        var left = new WDir(-f.Z, f.X);

        _center = origin + shift * left;

        // Render + forbid using normal rectangle primitives
        const float Back = 20f;

        _aoeShape = new AOEShapeRect(Len + Back, halfW);

        // SDRect signature: (origin, dir, lenFront, lenBack, halfWidth)
        _forbidShape = new SDRect(_center, f, Len, Back, halfW);

        // IMPORTANT: because we extended backwards, shift the rendered center back by Back/2 so the drawn rect
        // spans [-Back .. +Len] along forward axis instead of [0 .. Len+Back].
        // (AOEShapeRect is centered.)
        _center -= f * (Back * 0.5f);

    }

  private Angle WallAlignedRotation(WPos bossPos, uint iconID)
{
    var c = Module.Center;
    var d = bossPos - c;

    // North boundary (top edge)
    if (d.Z < 0 && MathF.Abs(d.Z) >= MathF.Abs(d.X))
        return 90.Degrees(); // east/west axis

    // Southeast boundary (bottom-right)
    if (d.X > 0 && d.Z > 0)
        return 225.Degrees(); // NW/SE axis (this is the one you said works perfectly)

    // Fallbacks (should rarely be hit for this mechanic)
    if (MathF.Abs(d.Z) >= MathF.Abs(d.X))
        return 90.Degrees(); // default E/W
    else
        return 0.Degrees();  // default N/S
}
}
