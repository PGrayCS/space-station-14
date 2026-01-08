using Content.Server.StationEvents.Events;
using Content.Shared.Atmos;
using Robust.Shared.Map;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(GasLeakRule))]
public sealed partial class GasLeakRuleComponent : Component
{
    /// <summary>
    ///     Possible gases that can be leaked by this event.
    /// </summary>
    [DataField]
    public Gas[] LeakableGases =
    {
        Gas.Ammonia,
        Gas.Plasma,
        Gas.Tritium,
        Gas.Frezon,
        Gas.WaterVapor, // the fog
    };

    /// <summary>
    ///     Running cooldown of how much time until another leak.
    /// </summary>
    public float TimeUntilLeak;

    /// <summary>
    ///     How long between more gas being added to the tile.
    /// </summary>
    [DataField]
    public float LeakCooldown = 1.0f;

    // Event variables
    public EntityUid TargetStation;
    public EntityUid TargetGrid;
    public Vector2i TargetTile;
    public EntityCoordinates TargetCoords;
    public bool FoundTile;
    public Gas LeakGas;
    public float MolesPerSecond;

    /// <summary>
    ///     Minimum moles of gas leaked per second.
    /// </summary>
    [DataField]
    public int MinimumMolesPerSecond = 80;

    /// <summary>
    ///     Maximum moles of gas leaked per second. Don't want to make it too fast to give people time to flee.
    /// </summary>
    [DataField]
    public int MaximumMolesPerSecond = 200;

    /// <summary>
    ///     Minimum total moles of gas to leak during the event.
    /// </summary>
    [DataField]
    public int MinimumGas = 1000;

    /// <summary>
    ///     Maximum total moles of gas to leak during the event.
    /// </summary>
    [DataField]
    public int MaximumGas = 4000;

    /// <summary>
    ///     Chance that a spark will ignite the gas at the end of the event.
    /// </summary>
    [DataField]
    public float SparkChance = 0.05f;
}
