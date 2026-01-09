using System.ComponentModel.Design.Serialization;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualBasic;

namespace BossMod.Stormblood.Raid.O1NAlteRoite;
public enum OID : uint
{
    Boss = 0x1A6F,
    Helper = 0x233C,
    BallOfFire = 0x1A71, // R1.000, x0 (spawn during fight)
}
public enum AID : uint
{
    Attack = 872, // Boss->player, no cast, single-target
    WyrmTail = 9174, // Boss->player, no cast, single-target
    Flame = 9181, // Boss->self, no cast, ???
    Roar = 9180, // Boss->self, 4.0s cast, range 100 circle
    Burn = 9173, // 1A71->self, 1.0s cast, range 8 circle
    BreathWing = 9182, // Boss->self, 5.0s cast, single-target
    TwinBolt = 9175, // Boss->self, 5.0s cast, single-target
    Clamp = 9186, // Boss->self, 3.0s cast, range 9+R width 10 rect
    FlashFreeze = 9183, // Boss->self, no cast, single-target
    Levinbolt = 9177, // Boss->self, 5.0s cast, single-target
    Blaze = 9185, // Boss->players, 5.0s cast, range 6 circle
    TheClassicalElements = 9184, // Boss->self, 5.0s cast, single-target
    Downburst = 7896, // Boss->self, 5.0s cast, single-target
}
sealed class Roar(BossModule module) : Components.RaidwideCast(module, (uint)AID.Roar);
sealed class ThinIce(BossModule module) : Components.ThinIce(module, 6f, true, (uint)SID.ThinIce, true);
sealed class Burn(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Burn, new AOEShapeCircle(8f));
sealed class BreathWing(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.BreathWing, 10f, kind: Kind.DirForward, stopAtWall: true);
    sealed class WyrmTail(BossModule module) : Components.SingleTargetCast(module, (uint)AID.WyrmTail);
    sealed class Downburst(BossModule module) : Components.VoidzoneAtCastTarget(module, 6f, (uint)AID.Downburst, m => new[] { m.PrimaryActor });
sealed class TwinBolt(BossModule module) : Components.SingleTargetCast(module, (uint)AID.TwinBolt);
sealed class Blaze(BossModule module) : Components.StackTogether(module, iconId: 96, activationDelay: 5.0f, radius: 6f);
    sealed class Levinbolt(BossModule module) : Components.GenericStackSpread(module);
sealed class Clamp(BossModule module) : Components.Cleave(module, (uint)AID.Clamp, new AOEShapeRect(9f, 10f));
sealed class Flame(BossModule module) : Components.RaidwideCast(module, (uint)AID.Flame);

public enum SID : uint
{
    ThinIce = 911, // none->player, extra=0x96
}

public enum TetherID : uint
{
    Tether_chn_raikou1s = 21, // player->player
}

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
            .ActivateOnEnter<Downburst>()
            .ActivateOnEnter<TwinBolt>()
            .ActivateOnEnter<Levinbolt>()
            .ActivateOnEnter<Clamp>()
            .ActivateOnEnter<Flame>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP,
ObjectIDType = typeof(OID),
ActionIDType = typeof(AID), // replace null with typeof(AID) if applicable
StatusIDType = typeof(SID), // replace null with typeof(SID) if applicable
TetherIDType = typeof(TetherID), // replace null with typeof(TetherID) if applicable
IconIDType = null, // replace null with typeof(IconID) if applicable
Contributors = "JoeSparkx",
GroupType = BossModuleInfo.GroupType.CFC,
GroupID = 252,
NameID = 5629)]
public class O1NAlteRoite(WorldState ws, Actor primary) : BossModule(ws, primary, new(00, 00), new ArenaBoundsCircle(20));



