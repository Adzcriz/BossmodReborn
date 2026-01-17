namespace BossMod.Dawntrail.Raid.M12NLindwurm;

sealed class FleshTele(BossModule module) : BossComponent(module)
{
    private readonly RavenousReach _reach = module.FindComponent<RavenousReach>()!;
    private readonly Burst _burst = module.FindComponent<Burst>()!;
    private bool _active = false;
    private readonly Dictionary<ulong, bool> _jumps = [];

    private RelSimplifiedComplexPolygon _poly;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Shockwave)
        {
            _active = false;
            _jumps.Clear();
        }
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.FleshTimer)
        {
            if (Arena.Bounds is ArenaBoundsCustom arena)
            {
                _poly = arena.Polygon.Offset(-1f); // pretend polygon is 1y smaller than real for less suspect knockbacks
            }
            else
            {
                _poly = Arena.Bounds.ShapeSimplified.Offset(-1f);
            }
            _active = true;
        }
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.FleshForward or (uint)SID.FleshBack)
        {
            _jumps[actor.InstanceID] = status.ID is (uint)SID.FleshForward;
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.FleshForward or (uint)SID.FleshBack)
        {
            if (_jumps.ContainsKey(actor.InstanceID))
            {
                _jumps.Remove(actor.InstanceID);
            }
        }
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor.OID == Module.PrimaryActor.OID)
        {
            _active = false;
            _jumps.Clear();
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (!_active)
            return;
        if (!_jumps.ContainsKey(pc.InstanceID))
            return;

        var tp = GetTeleport(pc, _jumps[pc.InstanceID]);
        Arena.ActorProjected(tp.from, tp.to, tp.rotation, Colors.Danger);
        Arena.AddLine(tp.from, tp.to);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (!_active)
            return;

        if (!_jumps.ContainsKey(actor.InstanceID))
            return;

        var tp = GetTeleport(actor, _jumps[actor.InstanceID]);
        if (DestinationUnsafe(tp.to))
            hints.Add("About to teleport into danger!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active)
            return;

        if (!_jumps.ContainsKey(actor.InstanceID))
            return;

        var rot = actor.Rotation;
        var dir = rot.ToDirection() * 15f * (_jumps[actor.InstanceID] ? 1f : -1f);

        var tp = GetTeleport(actor, _jumps[actor.InstanceID]);
        if (DestinationUnsafe(tp.to))
        {
            hints.ForbiddenDirections.Add((dir.ToAngle(), 175f.Degrees(), WorldState.FutureTime(5d)));
        }

        hints.AddForbiddenZone(new SDKnockbackInComplexPolygonFixedDirection(tp.from, dir, _poly), WorldState.FutureTime(5d));

        var reach = _reach.ActiveCasters;
        if (reach.Length > 0)
        {
            hints.AddForbiddenZone(new SDKnockbackInComplexPolygonFixedDirection(tp.from, dir, _poly), _reach.ActiveAOEs(slot, actor)[0].Activation);
            return;
        }

        // burst doesn't happen during broken arena
        var burst = _burst.AOEs;
        if (burst.Count > 0)
        {
            hints.AddForbiddenZone(new SDKnockbackInAABBRectFixedDirection(Arena.Center, dir, 20f, 15f));
            return;
        }
    }

    private bool DestinationUnsafe(WPos pos)
    {
        if (!Arena.InBounds(pos))
            return true;

        var reach = _reach.ActiveCasters;
        var reachLen = reach.Length;
        for (var i = 0; i < reachLen; ++i)
        {
            if (reach[i].Check(pos))
                return true;
        }

        var burst = CollectionsMarshal.AsSpan(_burst.AOEs);
        var burstLen = burst.Length;
        for (var i = 0; i < burstLen; ++i)
        {
            if (burst[i].Check(pos))
                return true;
        }

        return false;
    }

    private const float approxHitBoxRadius = 0.499f;
    private const float maxIntersectionError = 0.5f - approxHitBoxRadius;

    private (WPos from, WPos to, Angle rotation) GetTeleport(Actor actor, bool isForward)
    {
        var from = actor.Position;
        var rot = actor.Rotation;
        var dir = rot.ToDirection() * (isForward ? 1f : -1f);

        // stopAfterWall
        var distance = Math.Min(15f, Arena.IntersectRayBounds(from, dir) + maxIntersectionError);
        var to = from + distance * dir;

        return (from, to, rot);
    }
}
