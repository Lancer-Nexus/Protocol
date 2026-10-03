# NPC transfer diagnostics

Read-only .NET 10 helper for stored NPC snapshots and private QUIC/mTLS handshake checks.
It references the repository's Protocol project and uses its explicit MessagePack contracts.

```bash
dotnet run --project tools/NpcTransferDiagnostics -- snapshot /path/to/instance-npc-transfer
dotnet run --project tools/NpcTransferDiagnostics -- snapshot /path/to/transfer.source.msgpack
dotnet run --project tools/NpcTransferDiagnostics -- quic /path/to/source-llserver.json 127.0.0.3 26455 li02
```

Run from the Protocol repository. The `snapshot` command reports NPC identities,
ownership versions, snapshot hashes and steering/physics state without changing the files.
The `quic` command loads the source instance's certificate and CA from its LLServer
configuration. Certificate paths must be absolute or relative to the current directory.
Provide the PFX password through `LANCER_NEXUS_NPC_TRANSFER_CERT_PASSWORD`, for example
by sourcing the private instance environment file. Never put the password in command arguments.

The probe checks the target's certificate name, custom CA chain and Server Authentication
usage, and negotiates `lancer-nexus-npc-transfer/1`. It opens no snapshot stream and
changes no Coordinator journal or NPC lease. The target may log the connection closing
before its first stream; that is expected for this handshake probe. A successful probe
proves transport authentication only, not a complete NPC transfer or target activation.
