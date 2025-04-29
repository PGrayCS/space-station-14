using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Shared.Radio.Components;
using Content.Server.Starlight.TTS;
using Content.Server.VoiceMask;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.PDA;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Content.Shared.StatusIcon;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared; // Added for RadioChimeComponent
using Content.Server.Radio.Components; // Added for ActiveRadioComponent

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClothingSystem _clothingSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioMessage(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var meta = MetaData(messageSource);
        var entityName = meta?.EntityName ?? string.Empty;
        var evt = new TransformSpeakerNameEvent(messageSource, entityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        if (string.IsNullOrEmpty(name))
            name = entityName;
        if (name == null)
            name = string.Empty;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.TryIndex(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        // start 🌟Starlight🌟

        var iconId = "JobIconNoId";
        var jobName = "";
        SoundSpecifier? soundPath = null;

        if (_accessReader.FindAccessItemsInventory(messageSource, out var items))
        {
            foreach (var item in items)
            {
                // ID Card
                if (TryComp<IdCardComponent>(item, out var id))
                {
                    iconId = id.JobIcon;
                    jobName = id.LocalizedJobTitle;
                    break;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && TryComp(pda.ContainedId, out id))
                {
                    iconId = id.JobIcon;
                    jobName = id.LocalizedJobTitle;
                    break;
                }
            }
        }
        
        if (HasComp<BorgChassisComponent>(messageSource) || HasComp<BorgBrainComponent>(messageSource))
        {
            iconId = "JobIconBorg";
            jobName = Loc.GetString("job-name-borg");
        }
        
        if (HasComp<StationAiHeldComponent>(messageSource))
        {
            iconId = "JobIconStationAi";
            jobName = Loc.GetString("job-name-station-ai");
        }

        // Play chime sound if player is wearing a headset with a RadioChimeComponent
        Filter filter = Filter.Empty();

        if (_inventorySystem.TryGetSlotEntity(messageSource, "ears", out var headsetEntity))
        {
            Logger.Debug($"Headset entity found in ears slot: {headsetEntity.Value}");

            if (TryComp<RadioChimeComponent>(headsetEntity.Value, out var radioChime) && radioChime.ChimeSound != null)
            {
                Logger.Debug($"RadioChimeComponent found with sound: {radioChime.ChimeSound}");
                soundPath = radioChime.ChimeSound;
            }
            else
            {
                Logger.Debug("RadioChimeComponent not found or ChimeSound is null on headset entity.");
            }

            if (soundPath != null)
            {
            var players = EntityQueryEnumerator<ActorComponent>();
            int count = 0;
            while (players.MoveNext(out var entity, out var actor))
            {
                Logger.Debug($"Checking player entity {entity} with ActorComponent");
                if (_inventorySystem.TryGetSlotEntity(entity, "ears", out var headsetSlotEntity))
                {
                    Logger.Debug($"Player entity {entity} has headset entity {headsetSlotEntity.Value} in ears slot");
                    if (TryComp<RadioChimeComponent>(headsetSlotEntity.Value, out var radioChime2) && radioChime2.ChimeSound != null)
                    {
                        Logger.Debug($"Headset entity {headsetSlotEntity.Value} has RadioChimeComponent with sound {radioChime2.ChimeSound}");
                        filter = filter.AddPlayer(actor.PlayerSession);
                        count++;
                    }
                    else
                    {
                        Logger.Debug($"Headset entity {headsetSlotEntity.Value} does not have RadioChimeComponent or ChimeSound is null");
                    }
                }
                else
                {
                    Logger.Debug($"Player entity {entity} does not have headset entity in ears slot");
                }
            }
            Logger.Debug($"Number of players with headset and RadioChimeComponent added to filter: {count}");

                _audio.PlayGlobal(soundPath, filter, true, AudioParams.Default.WithVolume(-7f));
                Logger.Debug("Played global chime sound.");
            }
        }
        else
        {
            Logger.Debug("No headset entity found in ears slot.");
        }

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", $"[icon src=\"{iconId}\" tooltip=\"{jobName}\"] {name}"),  // 🌟Starlight🌟
            ("message", content));

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };
        var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg, []);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }
        RaiseLocalEvent(new RadioSpokeEvent
        {
            Source = messageSource,
            Message = message,
            Receivers = [.. ev.Receivers]
        });

        if (soundPath != null && canSend)
        {
            _audio.PlayGlobal(soundPath, filter, true, AudioParams.Default.WithVolume(-7f));
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(chat);
        _messages.Remove(message);
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }
}
