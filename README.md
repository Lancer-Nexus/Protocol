# Lancer Nexus Protocol

AdminContracts defines bounded read-only AdminQuery commands (help/status/instances/instance), admitted GameAdminQueryRequest, Gateway-attested AuthorizedAdminQueryRequest and private AdminQueryResponse. AdminActionContracts adds typed MessagePack contracts for message, kick, ban, unban, drain, move, teleport and transfer requests plus correlated results. These mutation contracts are a wire foundation only: Gateway identity attestation, current permission/scope checks, confirmation validation, idempotent execution, presence resolution and durable audit remain service responsibilities. Explicit keys and closed enums prevent arbitrary command text from crossing service boundaries. Structural validation does not authorize an actor or prove the target's current state.

`FlHookEventV1` defines player and character lifecycle events plus local system changes, with per-instance monotonic sequence numbers within a `ProducerEpoch` and UTC timestamps. Character events carry an optional positive stable `CharacterId` at MessagePack key 7; the epoch is key 8, and prior seven-field events remain readable. `FlHookEventReplayRequestV1` bounds replay requests to 500 events and can include the prior producer epoch. `FlHookEventReplayResultV1` includes current and requested epochs, the requested cursor, and validates ordering, instance identity and whether retained history or a producer restart caused a gap. Message IDs 302–304 and capability `flhook_events_v1` identify the event, replay-request and replay-result route. These contracts and IDs do not implement event production, transport, durable retention or game-state authority. An empty history has an empty sequence range. A gap requires consumers to reconcile against an authoritative snapshot before consuming replayed events.

## Dynamic market contracts

`dynamic_market_v1` adds bounded, keyset-paginated system market snapshots and confirmed trade-effect events. Snapshot requests pin later pages to the first page's `SystemRevision`; the service returns `RevisionExpired` instead of mixing market states if that revision is no longer available. Rows expose reputation-neutral unit prices, stock, target stock, per-market revision and buy/sell flags. Message IDs 310–311 carry snapshot reads.

`MarketTradeEventV1` carries only the completed trade's market identity, direction and quantity plus its instance, producer epoch and sequence. It intentionally carries no client price, reputation, credits or character identity. The service deduplicates by both `EventId` and `(InstanceId, ProducerEpoch, Sequence)`; an identical retry returns `Duplicate`, while identity reuse with a changed payload is a conflict. `Unavailable` is retryable; only `Applied` and `Duplicate` acknowledgements allow an event producer to advance its durable cursor. Message IDs 312–313 carry event ingestion and acknowledgement. These contracts do not authorize or book credits/inventory; market storage, trade outbox, source authentication, quote pricing and the Gateway booking boundary remain service work. The capability must not be advertised until those consumers are implemented and verified.

`MarketTradeReservationRequestV1` binds one Gateway-generated `TradeId` to market, good, side, quantity and the expected market revision. Repeating the same trade payload is idempotent; reusing its ID with changed fields is a conflict at the service boundary. `MarketTradeResolutionRequestV1` commits or aborts that reservation. Responses distinguish `Reserved`, `Committed`, `Aborted`, `Conflict`, `Rejected` and retryable `Unavailable`; `QuoteUnitPrice` is the first unit's marginal price and `TotalPrice` sums each unit's price over the quantity. Message IDs 314–317 identify the request/response pairs. These contracts do not authorize Gateway identity, apply reputation, or persist reservations; unresolved reservations must not expire solely by timeout while Gateway's durable decision is unknown.

`MarketTradeQuoteReservationRequestV1` (Message ID 326) binds a trade reservation to the character, market faction, quoted market revision and expected Gateway reputation revision, and carries the server-derived reputation factor plus expected first-unit and total prices. Coordinator rereads reputation through its authenticated Gateway service channel, verifies the revision and derives the factor from the faction configured on the locked market row. It then recomputes exact totals under that row lock. The response (Message ID 327) distinguishes stale market/reputation revisions and echoes the applied faction and revision on success. The legacy reservation contract remains available for controlled migration but must not be used for player-facing trade booking.

`MarketTradeBundleReservationRequestV1` (Message ID 328) groups every buy and sell effect of one ship/package exchange under one `tradeId`, character, market, system and reputation revision. It allows at most 64 canonical `(goodId, side)` lines; duplicate or unsorted lines are rejected. Each line pins quantity, market revision, quote totals and the side-specific reputation factor. Message ID 329 returns either all matching reserved lines or a bundle-wide revision/validation failure; a stale market result identifies the exact good and side with its current revision to re-quote. The committed market effect is one `MarketTradeBundleEventV1` (Message ID 330) with quantity-only lines; its acknowledgement is Message ID 331. Existing trade resolution applies to the single bundle `tradeId`. Buy and sell lines for the same good remain distinct effects and must be reserved together atomically.

These bundle contracts define the wire boundary only. Gateway snapshot/ledger/outbox persistence, Coordinator multi-row reservation and event application, Client ship-dealer integration, recovery tests and runtime verification remain required before package trades can be enabled. The current Client keeps ship/package purchases fail-closed when `DynamicMarketV1` is enabled.

`MarketFactionReputationReadRequestV1` is an internal Coordinator-to-Gateway read bound to a character ID and optional expected SaveGame snapshot revision. A revision mismatch returns the current revision without faction values; the caller must fetch or rebuild its quote against that revision. Successful responses carry bounded values in the game's `[-1, 1]` range. Message IDs 318–319 identify this request/response pair. The contract does not define reputation discounts or authorize a trade.

`MarketCatalogReadRequestV1` and `MarketCatalogReadResponseV1` provide bounded, keyset-paginated reads of every seeded good, including goods with no active offer. The immutable seed SHA-256 is the catalog revision; continuation requests must pin it and receive `RevisionExpired` if it changes. Message IDs 320–321 identify the pair.

`MarketQuoteRequestV1` binds a bounded price preview to character, market, good, side, quantity and expected reputation revision; an optional expected market revision detects stale market state. The Coordinator obtains reputation from Gateway and never accepts client-supplied faction values. `MarketQuoteResponseV1` returns the exact faction/reputation used, factor, pricing policy version, marginal quantity total, both revisions and UTC expiry. It is a preview only and reserves no stock or credits. Message IDs 322–323 identify the pair.

`MarketGoodReadRequestV1` reads one exact `(systemId, marketId, goodId)` row and can pin both system and market revisions. Its response distinguishes a missing offer from unavailable storage and stale revisions; a found row must match all requested identifiers and the reported market revision. Message IDs 324–325 identify the pair. This read contract does not grant trade authority.

`MarketSystemsReadRequestV1` and `MarketSystemsReadResponseV1` provide a bounded, keyset-paginated cluster directory pinned to the immutable market seed revision. Each system reports its market count and static offer coverage; systems with no markets or offers remain visible. Coordinator and Gateway expose it through internal and public REST facades. The directory contains no player identity or reputation.

This repository contains versioned contracts for communication between the Lancer Nexus Client, Gateway, Coordinator, Agents and game instances.

## Protocol basis

- QUIC for private control and transfer channels
- TLS/mTLS through the QUIC stack
- MessagePack for compact, versioned binary messages
- Explicit protocol and capability versions
- Correlation IDs, idempotency keys and transfer IDs

The protocol defines envelopes, authentication metadata, registration, placement, leases, transfers, chat events and health messages. It does not contain service implementations.

`CharacterRosterRequestV1` and `CharacterRosterResultV1` define the session-bound game-instance roster call. Results contain Gateway character IDs and exact display names, with tombstones omitted by the service. `CharacterBootstrapRequestV1` stores a new character's initial SaveGame before it can be selected on another instance; `CharacterSnapshotPreviewRequestV1` reads an account-owned snapshot for character-menu reconstruction before lease acquisition. `CharacterDeletionRequestV1` and `CharacterDeletionResultV1` define the idempotent history-preserving deletion boundary; service implementations must reject live leases and staged transfers and retain the identity, lease, transfer and snapshot history. These contracts carry account/session identifiers for validation but do not authenticate the caller or authorize a deletion by themselves.

## NPC transfer contracts

The optional `npc_transfer_v1` capability identifies peers that understand the
NPC transfer messages. A transfer request reserves one target instance for a set
of stable NPC IDs; `FormationId` and `MissionRuntimeId` bind NPCs that must move
together. `NpcOwnershipLease.OwnershipVersion` is the fencing token for
authoritative simulation and persistence. Runtime snapshots are independently
versioned (`SnapshotSchemaVersion` and `RuntimeSchemaVersion`) and travel over
the authenticated instance-to-instance transfer channel; reject unsupported
versions and keep the target inactive until `Committed`.

`NpcRuntimeStateV1` RuntimeSchemaVersion 3 defines the core MessagePack state
shape: transform and velocities, health, loadout/equipment/cargo, structural
part health and destruction, autopilot target and elapsed time, AI
state/timers/random state, target NPC identity and mission runtime bytes.
Object references must be resolved by stable IDs on import. `ExtensionData`
allows engine-specific component state to be carried without changing the
shared contract; its contents still need a documented schema per producer.
NPC transfer snapshot schema 4 adds ordered formation members with stable NPC
or character IDs, member offsets, leader identity and the player's formation
position/target. The validator rejects partial NPC membership, duplicate
members and non-finite offsets; the source and target transfer the formation as
one unit.
The player mission runtime payload uses its own schema version. Version 4
includes active/completed triggers, condition storage, pending lines, objective
and random state, each mission label's spawned/alive/destroyed members, and a
data-reference descriptor for generated random missions, including the active
offer's display fields. Peers that do not
understand the mission payload version must reject the handoff.

`NpcPeerSnapshotTransfer` carries the serialized snapshot over a length-prefixed,
mTLS-protected QUIC peer stream. Payloads are limited to 15 MiB; the receiver
checks source/target certificate identities against the envelope and must compare
the snapshot with the Coordinator journal before staging it. The Client overlay
contains GameServer identity allocation, mTLS QUIC staging, simulation freeze,
target restore and coupled player/NPC activation. This protocol defines the
wire contract; deployments still need matching Coordinator migrations,
capabilities, certificates and server configuration before handoffs can run.
Coordinator journal recovery uses the versioned `NpcTransferRecoveryPageV1`
contract for keyset-paginated committed transfers; returned entries are limited
to groups whose complete NPC lease set remains active on the target instance.

## Development

The reusable [NPC transfer diagnostics tool](tools/NpcTransferDiagnostics/README.md)
inspects stored runtime snapshots and probes the private QUIC/mTLS handshake without
changing NPC leases or transfer journals.

```bash
dotnet restore tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj
dotnet build tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj --configuration Release --no-restore --warnaserror
dotnet test tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj --configuration Release --no-build
```

The implementation provides explicit MessagePack contracts for the envelope, capability hello/response, Agent and instance heartbeat/acknowledgement, placement, short-lived session-token claims and the idempotent transfer state machine. `SessionTokenClaimsValidator` checks identifiers, audience, key id, nonce and time validity; `JoinTicketClaimsValidator` additionally requires UTC timestamps, a valid optional character ID, a configured expected audience and nonnegative clock skew. Neither validator signs tokens or stores keys. `ClusterHandshakeNegotiator` checks the protocol version, computes the sorted intersection of capabilities and rejects locally required capabilities absent from the peer. Run it only after the transport has authenticated the peer; it does not replace QUIC TLS/mTLS. Field keys and message IDs are append-only; service implementations belong in the other repositories.

`PlacementRequest.IsValid()` is the shared structural gate used by Gateway and Coordinator. It requires nonempty request/session IDs, a nonblank system and idempotency key, a positive optional character ID, and bounded lengths for system, group/event, build, region and idempotency fields. It does not authorize the session, resolve account ownership, or choose an instance.

`ClientVersionHello` (keys 0–6) and `ClientVersionDecision` (keys 0–9) define the pre-login version exchange. Gateway owns the compatibility policy and issues the short-lived proof; the contract itself grants no permissions. `TransferStartRequest` lets an authenticated client name the character and target, while Gateway resolves the source instance from its authoritative lease. `TransferTicketClaims` binds short-lived transfer admission to the session, character, source and target instances, target system and current lease version. `TransferTicketVerificationRequest` includes the target server's actual instance ID so Gateway can enforce the ticket's target binding. Signing, replay handling and attachment checks remain service responsibilities.
`TransferTargetAcceptanceRequest` carries the validated transfer ticket and the target's proposed short-lived lease token after it has prepared the snapshot. The target instance identity comes from its per-instance authenticated Gateway connection, not this request body.

`CharacterLeaseAcquireRequestV1` binds post-selection acquisition to the admitted join ticket, selected character ID and a proposed 32-byte base64url lease token. Retries reuse that token so a lost response cannot rotate the lease. Gateway derives account, session and target instance from the ticket and authenticated instance key, then checks SQL ownership and active session state. `CharacterLeaseAcquireResultV1` returns the fencing version and expiry, never the token. These contracts do not implement token signing, instance authentication, SQL arbitration or LLServer persistence.
`TransferStatusResponse` is visible only to the authenticated source or target instance. `TransferSourceReleaseRequest` lets the source complete `SourceReleased` only after Gateway has committed the MySQL lease and Coordinator reports `Committed`.

Permission synchronization uses `PermissionRevisionChanged` and `PermissionRevisionAcknowledged` with append-only message IDs 300 and 301. Notices only invalidate local state; each instance reloads the authoritative SQL snapshot through Gateway and ACKs after activation. The ACK instance identity is derived from its authenticated connection. Never put permission snapshots, credentials or role mutations in Redis notices.

For the full migration, evaluation order and current process integration status, see [Administration's permission system guide](../Administration/docs/permission-system.md).

`InstanceHeartbeat.SystemIds` is optional at MessagePack key 10. Legacy ten-field messages decode with an empty list, retaining the primary `SystemId`; group-aware Coordinators use the full list without duplicating capacity.

`InstanceHeartbeat.NpcTransferEndpoint` is optional at key 11 and advertises the
private `quic://host:port` listener. NPC preparation and target resolution return
this endpoint when present; legacy heartbeats retain the game-host/configured-port
fallback. Instances sharing a host should use distinct QUIC ports.

`NpcMissionAuthorityRequestV1`/`NpcMissionAuthorityResultV1` define the private Coordinator-to-Gateway decision boundary. The shared transfer/mission ID, source, target, system and requested commit/abort must match. Commit requires a positive durable character lease version; abort carries no committed version. An accepted abort is a durable veto of later character commit, not a timeout or proof of an active source lease. Unknown schemas, unavailable authority or rejected/mismatched replies authorize no NPC phase change. Retry the same request after uncertain delivery; decisions are permanent and idempotent. Service authentication and SQL arbitration are Gateway responsibilities.

`npc_retirement_v1` adds bounded, independently fenced retirement batches.
`NpcRetirementRequestV1` carries a request ID, owner instance and up to 256 unique
NPC IDs with their current ownership versions and a terminal reason. A successful
retirement increments the fence and preserves the ID permanently. Pending
transfers prohibit retirement. Repeat the exact request ID/payload after uncertain
delivery; reusing the ID with another payload is rejected. Responses report each
entry separately, so a stale entry does not block unrelated valid entries.
`NpcOwnershipLease.IsRetired` is appended at key 4; older four-field leases decode
as active. Older consumers cannot safely reuse retired allocation results and must
negotiate retirement support before enabling this lifecycle path.

## NPC runtime checkpoints (contract foundation)

`npc_checkpoint_v1` defines `NpcCheckpointWriteRequestV1` and its response.
A write atomically stores live members, formations and optional MissionRuntime,
and retires terminal members. The union of survivors and retirements contains at
most 256 unique NPCs; expected revisions must match every ID and current ownership
version exactly. Formations reference survivors and, when present, the mission's
owning character. An empty survivor set is valid for terminal or mission-only
writes. Runtime schema 3 and mission schema 4 reuse the transfer validators.
The complete encoded write is limited to 15 MiB.

Checkpoint revisions are compare-and-swap counters within an ownership version;
revision zero means no checkpoint has been acknowledged in that fence. A successful
write increments each checkpoint revision once. Survivors retain ownership versions;
retirements increment ownership fences once and preserve permanent tombstones.
Mission revisions additionally bind a stable runtime ID and the current character
lease. SimulationTick is diagnostic and does not replace these fences. The service
must check authenticated instance identity, current character authority, no pending
NPC transfer, current revisions and group membership in durable arbitration.
Structural validation alone establishes none of these authority conditions.

The entire request succeeds or fails; there is no partial member acceptance.
An accepted response returns every member's resulting ownership/checkpoint revision
and, when a mission was included, the resulting mission revision. Rejected writes
return no revisions and change no state. Unknown delivery outcomes retry the exact
request ID and payload; the durable recorded result is permanent. Reusing an ID
with another payload is rejected. A known revision/transfer conflict requires a
new request after authoritative reconciliation, never blind replay of stale state.
Timeout does not release frozen NPCs or permit terminal actors to be restored.
Recovery must recheck current fences before activating the last acknowledged frame;
checkpoints do not guarantee restoration of unacknowledged simulation ticks.

These contracts are implemented and tested. Coordinator storage/endpoints,
Gateway character-authority arbitration, stable mission runtime identity and
GameServer checkpoint scheduling/recovery are not yet implemented. Do not advertise
the capability or connect terminal hooks until those paths are available.

## Event lifecycle contract foundation

`EventContracts.cs` defines the version-1 event definition, individual registration,
optimistic/idempotent lifecycle transition and frozen result-export messages. The
phase graph follows `planned -> registration -> provisioning -> ready -> locked ->
running -> result_export -> closed`, with cancellation before running and failure
recovery transitions before closure. Registration binds account, session, character
and current lease version; receiving services still re-check current identity,
eligibility, event capacity and event-instance assignment. Result exports contain
only bounded event points and battle counters, never normal credits or inventory.
Structural validation does not authorize actors or prove an event server's result;
Gateway attestation, authenticated service identity, durable idempotency, SQL result
validation and audit remain service responsibilities. Event service implementation,
Coordinator capacity reservation, reward policy and end-to-end return transfer remain
open.

`EventServerReservationRequestV1` and `EventServerReservationReleaseRequestV1` define
the authenticated Coordinator boundary for holding one ready instance's capacity for
an event's system, participant limit and scheduled window. Reservation decisions bind
the event to one instance and return a stable reservation ID; release decisions are
idempotent and require the matching event/reservation pair. These contracts do not
implement Coordinator persistence or lifecycle calls from Events.

## FLHook durable ingestion acknowledgement

`FlHookEventIngestResultV1` (message type 305) acknowledges the
`(InstanceId, ProducerEpoch, Sequence)` identity. `Stored` and `Duplicate` mean
that the receiver durably recorded the event and the producer may advance.
`Unavailable` is retryable with the same identity and payload. `Conflict` means
that identity was already recorded with different content; `Rejected` is
permanent input rejection. This is only the wire contract: a service must not
advertise `flhook_events_v1` as ready until it implements durable deduplication
and these outcomes.
