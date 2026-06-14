using System.Security.Cryptography;

namespace Platform.Realtime.Rooms;

internal interface IRoomCodeGenerator
{
    string Next();
}

/// <summary>
/// Generates short, human-friendly room codes (6 chars) from an unambiguous alphabet
/// (no 0/O/1/I) so they are easy to read aloud and type.
/// </summary>
internal sealed class RoomCodeGenerator : IRoomCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    public string Next()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
