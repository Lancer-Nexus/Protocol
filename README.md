# Lancer Nexus Protocol

AdminContracts defines bounded read-only AdminQuery commands (help/status/instances/instance), admitted GameAdminQueryRequest, Gateway-attested AuthorizedAdminQueryRequest and private AdminQueryResponse. Explicit MessagePack keys and a closed enum prevent executable text from crossing service boundaries. Parsers reject unknown commands, extra arguments and invalid identifiers. New contracts do not renumber existing messages. Responses carry correlation and outcome; queries carry account-scoped idempotency keys. Repeated queries must use current authorization and fresh status, not cached privileges.

This repository contains versioned contracts for communication between the Lancer Nexus Client, Gateway, Coordinator, Agents and game instances.

## Protocol basis

- QUIC for private control and transfer channels
- TLS/mTLS through the QUIC stack
- MessagePack for compact, versioned binary messages
- Explicit protocol and capability versions
- Correlation IDs, idempotency keys and transfer IDs

The protocol defines envelopes, authentication metadata, registration, placement, leases, transfers, chat events and health messages. It does not contain service implementations.

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

The implementation provides explicit MessagePack contracts for the envelope, capability hello/response, Agent and instance heartbeat/acknowledgement, placement, short-lived session-token claims and the idempotent transfer state machine. `SessionTokenClaimsValidator` checks identifiers, audience, key id, nonce and time validity; it does not sign tokens or store keys. `ClusterHandshakeNegotiator` checks the protocol version, computes the sorted intersection of capabilities and rejects locally required capabilities absent from the peer. Run it only after the transport has authenticated the peer; it does not replace QUIC TLS/mTLS. Field keys and message IDs are append-only; service implementations belong in the other repositories.

`ClientVersionHello` (keys 0–6) and `ClientVersionDecision` (keys 0–9) define the pre-login version exchange. Gateway owns the compatibility policy and issues the short-lived proof; the contract itself grants no permissions. `TransferStartRequest` lets an authenticated client name the character and target, while Gateway resolves the source instance from its authoritative lease. `TransferTicketClaims` binds short-lived transfer admission to the session, character, source and target instances, target system and current lease version. `TransferTicketVerificationRequest` includes the target server's actual instance ID so Gateway can enforce the ticket's target binding. Signing, replay handling and attachment checks remain service responsibilities.
`TransferTargetAcceptanceRequest` carries the validated transfer ticket and the target's proposed short-lived lease token after it has prepared the snapshot. The target instance identity comes from its per-instance authenticated Gateway connection, not this request body.
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
