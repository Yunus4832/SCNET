using System.Text;

using Game.Protocol.Connection;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static AdmissionChallengePackage ReadAdmissionChallenge(ProtocolReader reader)
    {
        var mode = (ServerAdmissionMode)reader.ReadByte();
        if (!Enum.IsDefined(mode)) { throw new InvalidDataException("Invalid server admission mode."); }

        var nonce = ReadAdmissionString(reader, 43);
        var expires = reader.ReadInt64();
        return mode == ServerAdmissionMode.External
            ? new AdmissionChallengePackage(
                mode,
                nonce,
                expires,
                ReadAdmissionString(reader, 128),
                ReadAdmissionString(reader, 2048),
                ReadAdmissionString(reader, 256)
            )
            : new AdmissionChallengePackage(mode, nonce, expires);
    }

    private static AdmissionProofPackage ReadAdmissionProof(ProtocolReader reader)
    {
        return new AdmissionProofPackage(
            ReadAdmissionString(reader, 43),
            ReadAdmissionString(reader, 1024),
            ReadAdmissionString(reader, 16384),
            reader.ReadBoolean()
        );
    }

    private static AdmissionResultPackage ReadAdmissionResult(ProtocolReader reader)
    {
        var nonce = ReadAdmissionString(reader, 43);
        var code = (AdmissionResultCode)reader.ReadByte();
        var retry = reader.ReadInt32();
        if (!Enum.IsDefined(code) || retry is < 0 or > 3600)
        {
            throw new InvalidDataException("Invalid server admission result.");
        }

        return new AdmissionResultPackage(nonce, code, retry);
    }

    private static void WriteAdmissionChallenge(AdmissionChallengePackage package, ProtocolWriter writer)
    {
        if (!Enum.IsDefined(package.Mode)) { throw new InvalidDataException("Invalid server admission mode."); }

        writer.Write((byte)package.Mode);
        WriteAdmissionString(writer, package.Nonce, 43);
        writer.Write(package.ExpiresAt);
        if (package.Mode == ServerAdmissionMode.External)
        {
            WriteAdmissionString(writer, package.AuthorityName, 128);
            WriteAdmissionString(writer, package.AuthorityUrl, 2048);
            WriteAdmissionString(writer, package.AuthorityPublicKey, 256);
        }
        else if (package.AuthorityName.Length > 0 ||
                 package.AuthorityUrl.Length > 0 ||
                 package.AuthorityPublicKey.Length > 0)
        {
            throw new InvalidDataException("Non-external admission cannot advertise an authority.");
        }
    }

    private static void WriteAdmissionProof(AdmissionProofPackage package, ProtocolWriter writer)
    {
        WriteAdmissionString(writer, package.Nonce, 43);
        WriteAdmissionString(writer, package.Password, 1024);
        WriteAdmissionString(writer, package.Approval, 16384);
        writer.Write(package.Cancel);
    }

    private static void WriteAdmissionResult(AdmissionResultPackage package, ProtocolWriter writer)
    {
        if (!Enum.IsDefined(package.Code) || package.RetryAfterSeconds is < 0 or > 3600)
        {
            throw new InvalidDataException("Invalid server admission result.");
        }

        WriteAdmissionString(writer, package.Nonce, 43);
        writer.Write((byte)package.Code);
        writer.Write(package.RetryAfterSeconds);
    }

    private static string ReadAdmissionString(ProtocolReader reader, int maximumCharacters)
    {
        var length = reader.Read7BitEncodedInt();
        if (length < 0 || length > maximumCharacters * 4)
        {
            throw new InvalidDataException("Admission field exceeds its size limit.");
        }

        var value = new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
        if (value.Length > maximumCharacters)
        {
            throw new InvalidDataException("Admission field exceeds its size limit.");
        }

        return value;
    }

    private static void WriteAdmissionString(ProtocolWriter writer, string value, int maximumCharacters)
    {
        if (value.Length > maximumCharacters)
        {
            throw new InvalidDataException("Admission field exceeds its size limit.");
        }

        writer.Write(value);
    }
}
