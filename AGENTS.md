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

## Verification

Maintain golden-message tests, compatibility tests for the previous protocol version and negative tests for malformed or replayed messages.
