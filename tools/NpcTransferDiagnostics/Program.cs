using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LancerNexus.Protocol;
using MessagePack;

try
{
    if (args.Length is 2 or 3 && args[0] == "snapshot" && (args.Length == 2 || args[2] == "--json"))
    {
        var path = args[1];
        var jsonOutput = args.Length == 3;
        var reports = new List<object>();
        var files = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.source.msgpack").Order(StringComparer.Ordinal).ToArray()
            : [path];
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var options = MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
            var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(bytes, options);
            NpcTransferContractValidator.Validate(snapshot);
            if (jsonOutput)
            {
                reports.Add(new
                {
                    snapshot.TransferId, snapshot.TargetSystemId, snapshot.TargetArrivalObject,
                    snapshot.MissionRuntimeId,
                    SnapshotSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                    Mission = snapshot.MissionRuntimeState.Length == 0 ? null :
                        MessagePackSerializer.Deserialize<NpcMissionRuntimeStateV1>(snapshot.MissionRuntimeState, options),
                    Npcs = snapshot.Npcs.Select(npc => new
                    {
                        npc.NpcId, npc.OwnershipVersion, npc.SystemId,
                        State = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(npc.RuntimeState, options)
                    }).ToArray()
                });
                continue;
            }
            Console.WriteLine($"Transfer {snapshot.TransferId:D}: target={snapshot.TargetSystemId}, NPCs={snapshot.Npcs.Length}, SHA256={Convert.ToHexString(SHA256.HashData(bytes))}");
            foreach (var npc in snapshot.Npcs)
            {
                var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(npc.RuntimeState, options);
                using var ai = JsonDocument.Parse(state.Ai.ExtensionData);
                Console.WriteLine($"  NPC {npc.NpcId:D}: name={state.Nickname}, ownerVersion={npc.OwnershipVersion}");
                foreach (var name in new[] { "Steering", "Physics" })
                    Console.WriteLine($"  {name}={ai.RootElement.GetProperty(name).GetRawText()}");
            }
        }
        if (jsonOutput)
            Console.WriteLine(JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    if (args is ["quic", var configPath, var address, var portText, var targetInstance])
    {
        if (!OperatingSystem.IsLinux() || !QuicConnection.IsSupported)
            throw new PlatformNotSupportedException("The QUIC probe requires Linux and MsQuic.");
        if (!IPAddress.TryParse(address, out var ip) || !int.TryParse(portText, out var port) || port is < 1 or > 65535)
            throw new ArgumentException("A literal IP address and a port between 1 and 65535 are required.");
        using var config = JsonDocument.Parse(File.ReadAllBytes(configPath));
        var root = config.RootElement;
        var password = Environment.GetEnvironmentVariable("LANCER_NEXUS_NPC_TRANSFER_CERT_PASSWORD");
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            root.GetProperty("NpcTransferServerCertificate").GetString()!, password,
            X509KeyStorageFlags.EphemeralKeySet);
        using var ca = X509Certificate2.CreateFromPem(
            File.ReadAllText(root.GetProperty("NpcTransferClientCaCertificate").GetString()!));
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = new IPEndPoint(ip, port),
            DefaultStreamErrorCode = 0x200,
            DefaultCloseErrorCode = 0x201,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol("lancer-nexus-npc-transfer/1")],
                TargetHost = targetInstance,
                ClientCertificates = new X509CertificateCollection { certificate },
                RemoteCertificateValidationCallback = (_, cert, _, errors) =>
                {
                    if (cert is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                        return false;
                    using var remote = new X509Certificate2(cert);
                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(ca);
                    chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    Console.WriteLine($"Peer DNS={remote.GetNameInfo(X509NameType.DnsName, false)}");
                    return chain.Build(remote);
                }
            }
        }, cancel.Token);
        Console.WriteLine($"Negotiated ALPN={connection.NegotiatedApplicationProtocol}; endpoint={connection.RemoteEndPoint}");
        return 0;
    }

    Console.Error.WriteLine("Usage: NpcTransferDiagnostics snapshot <source snapshot file or directory> [--json]");
    Console.Error.WriteLine("       NpcTransferDiagnostics quic <source LLServer config> <target IP> <port> <target instance ID>");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
    return 1;
}
