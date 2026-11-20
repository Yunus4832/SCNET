using Game.Protocol.Connection;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class AdmissionCodecTest
{
    [Theory]
    [InlineData(ServerAdmissionMode.Open)]
    [InlineData(ServerAdmissionMode.Password)]
    [InlineData(ServerAdmissionMode.External)]
    public void ChallengesRoundTripWithoutSharingServerSecrets(ServerAdmissionMode mode)
    {
        var challenge = mode == ServerAdmissionMode.External
            ? new AdmissionChallengePackage(
                mode,
                new string('n', 43),
                12345,
                "Provider",
                "https://auth.example/",
                "public-key"
            )
            : new AdmissionChallengePackage(mode, new string('n', 43), 12345);
        var restored = Assert.Single(
            ProtocolPacketCodec.Decode(ProtocolPacketCodec.Encode([challenge], CompressionPolicy.None))
        );
        Assert.Equal(challenge, restored);
    }

    [Fact]
    public void CredentialsAndResultsRoundTripAndCredentialDiagnosticsAreRedacted()
    {
        var proof = new AdmissionProofPackage(new string('n', 43), "test-password", "test-approval");
        var result = new AdmissionResultPackage(proof.Nonce, AdmissionResultCode.RateLimited, 2);
        var restored = ProtocolPacketCodec.Decode(ProtocolPacketCodec.Encode([proof, result], CompressionPolicy.None));
        Assert.Equal(proof, restored[0]);
        Assert.Equal(result, restored[1]);
        Assert.DoesNotContain(proof.Password, proof.ToString());
        Assert.DoesNotContain(proof.Approval, proof.ToString());
    }

    [Fact]
    public void OversizedFieldsAndInvalidModesDoNotEnterTheWire()
    {
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Encode(
                [new AdmissionProofPackage(new string('n', 43), new string('p', 1025))],
                CompressionPolicy.None
            )
        );
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Encode(
                [new AdmissionChallengePackage((ServerAdmissionMode)255, "nonce", 0)],
                CompressionPolicy.None
            )
        );
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Encode(
                [new AdmissionChallengePackage(ServerAdmissionMode.Open, "nonce", 0, AuthorityPublicKey: "unexpected")],
                CompressionPolicy.None
            )
        );
    }
}
