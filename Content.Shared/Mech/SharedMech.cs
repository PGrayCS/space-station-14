﻿using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Mech;

[Serializable, NetSerializable]
public enum MechVisuals : byte
{
    Open, //whether or not it's open and has a rider
    Broken, //if it broke and no longer works.
    Light, //if lights are enabled
    Siren //if siren are enabled
}

[Serializable, NetSerializable]
public enum MechAssemblyVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum MechVisualLayers : byte
{
    Base,
    Light,
    Siren
}

[Serializable, NetSerializable]
public enum EquipmentType : byte
{
    Active,
    Passive
}

/// <summary>
/// Event raised on equipment when it is inserted into a mech
/// </summary>
[ByRefEvent]
public readonly record struct MechEquipmentInsertedEvent(EntityUid Mech)
{
    public readonly EntityUid Mech = Mech;
}

/// <summary>
/// Event raised on equipment when it is removed from a mech
/// </summary>
[ByRefEvent]
public readonly record struct MechEquipmentRemovedEvent(EntityUid Mech)
{
    public readonly EntityUid Mech = Mech;
}

/// <summary>
/// Raised on the mech equipment before it is going to be removed.
/// </summary>
[ByRefEvent]
public record struct AttemptRemoveMechEquipmentEvent()
{
    public bool Cancelled = false;
}

public sealed partial class MechToggleEquipmentEvent : InstantActionEvent
{
}

public sealed partial class MechOpenUiEvent : InstantActionEvent
{
}

public sealed partial class MechEjectPilotEvent : InstantActionEvent
{
}

public sealed partial class MechToggleInternalsEvent : InstantActionEvent
{
}

public sealed partial class MechToggleSirensEvent : InstantActionEvent
{
}

public sealed partial class MechToggleThrustersEvent : InstantActionEvent
{
}

public sealed partial class MechToggleNightVisionEvent : InstantActionEvent
{
}

[ByRefEvent]
public readonly record struct BeforePilotEjectEvent(EntityUid Mech, EntityUid Pilot)
{
    public readonly EntityUid Mech = Mech;

    public readonly EntityUid Pilot = Pilot;
}

[ByRefEvent]
public readonly record struct BeforePilotInsertEvent(EntityUid Mech, EntityUid Pilot)
{
    public readonly EntityUid Mech = Mech;

    public readonly EntityUid Pilot = Pilot;
}