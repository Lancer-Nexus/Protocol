# AGENTS.md – Lancer Nexus Protocol

## Mission

Maintain stable, explicit and interoperable contracts for the cluster.

## MVP architecture baseline

- Protocol is implemented before Gateway and Coordinator service logic and defines the versioned envelope, capability negotiation, placement and transfer contracts.
- The authoritative transfer states are `Requested -> Reserved -> Prepared -> SourceFrozen -> TargetAccepted -> Committed -> SourceReleased`; requests require idempotency, expiry, replay and recovery semantics.
- Contracts carry the identifiers needed for MySQL lease fencing, including the monotonic `lease_version`; only the committed target may persist character changes with the new version.
- Gateway owns identity, Coordinator owns placement, game instances own live simulation, and Redis is never an authoritative data source.

## Rules

- Use explicit MessagePack keys; do not rely on contractless serialization.
- Never reuse a field number for a different meaning.
- Add fields compatibly and define behavior for unknown fields.
- Version envelopes and capabilities independently from service versions.
- Include correlation, request, transfer and idempotency identifiers where required.
- Do not place passwords or long-lived private credentials in messages.
- Define timeout, retry, replay and error semantics for every state-changing operation.
- Keep protocol models free of service-specific database or UI dependencies.

## Working-model escalation

- If a task requires complex reasoning beyond the current model's reliable scope, ask the user whether switching to a stronger model is desired before continuing.
- Do not switch models silently or broaden the task because a stronger model may be useful.

## Verification

AdminQuery is a closed read-only wire contract with explicit MessagePack keys, correlation and account-scoped idempotency identifiers. Reject unknown kinds, malformed identifiers and extra chat arguments; never carry executable raw admin text. Admission identity is attested by Gateway and does not itself grant administrator rights. Repeated queries require current authorization and fresh status in the consuming service.

Maintain golden-message tests, compatibility tests for the previous protocol version and negative tests for malformed or replayed messages.

## Nexus baseline system groups

The base Nexus topology uses eight game instances, one per group: BR01-BR06 (`br-01`), BW01-BW10 (`bw-01`), EW01-EW05 (`ew-01`), IW01-IW06 (`iw-01`), KU01-KU06 (`ku-01`), LI01-LI05 (`li-01`), RH01-RH05 (`rh-01`), and `mixed-01` for all remaining registered systems. System nicknames are compared case insensitively and emitted lowercase. Folder names are not always world nicknames: `fp7` contains `fp7_system`; `intro` and `miners` are asset directories, not registered worlds.
InstanceHeartbeat adds optional SystemIds at MessagePack key 10. Old ten-field messages must decode to an empty set, preserving legacy primary-SystemId routing. Do not renumber existing keys or multiply instance capacity per reported system.
