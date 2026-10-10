# LancerNexus.Protocol

Versioned MessagePack contracts shared by Lancer Nexus services and game
clients. Wire contracts use explicit keys and append-only message identifiers.

This package defines message shape and structural validation only. It does not
authenticate peers, authorize actions, issue tokens, or persist service state.
Consumers must enforce those policies at their service boundary.

The `LancerNexus.Cluster` package depends on the matching Protocol package
version. Keep producer and consumer package versions pinned and update protocol
contracts before integrating new service behavior.
