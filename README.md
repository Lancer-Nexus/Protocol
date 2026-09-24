# Lancer Nexus Protocol

This repository contains versioned contracts for communication between the Lancer Nexus Client, Gateway, Coordinator, Agents and game instances.

## Protocol basis

- QUIC for private control and transfer channels
- TLS/mTLS through the QUIC stack
- MessagePack for compact, versioned binary messages
- Explicit protocol and capability versions
- Correlation IDs, idempotency keys and transfer IDs

The protocol defines envelopes, authentication metadata, registration, placement, leases, transfers, chat events and health messages. It does not contain service implementations.

## Development

```bash
dotnet restore tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj
dotnet build tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj --configuration Release --no-restore --warnaserror
dotnet test tests/LancerNexus.Protocol.Tests/LancerNexus.Protocol.Tests.csproj --configuration Release --no-build
```

The implementation provides explicit MessagePack contracts for the envelope, capability hello/response, Agent and instance heartbeat/acknowledgement, placement, short-lived session-token claims and the idempotent transfer state machine. `SessionTokenClaimsValidator` checks identifiers, audience, key id, nonce and time validity; it does not sign tokens or store keys. `ClusterHandshakeNegotiator` checks the protocol version, computes the sorted intersection of capabilities and rejects locally required capabilities absent from the peer. Run it only after the transport has authenticated the peer; it does not replace QUIC TLS/mTLS. Field keys and message IDs are append-only; service implementations belong in the other repositories.

`ClientVersionHello` (keys 0–6) and `ClientVersionDecision` (keys 0–9) define the pre-login version exchange. Gateway owns the compatibility policy and issues the short-lived proof; the contract itself grants no permissions.
