using System.Security.Cryptography;
using System.Text;
using Fuse.Core.Helpers;

namespace Fuse.Core.Areas.ScrumPoker;

public sealed class InMemoryScrumPokerStore : IScrumPokerStore
{
    public const int MaxParticipantsPerRoom = 20;
    public const int MaxDisplayNameLength = 50;
    public static readonly TimeSpan DefaultRoomLifetime = TimeSpan.FromHours(4);

    private const int RoomCodeLength = 8;
    private const int ParticipantTokenLength = 32;
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly object _roomsLock = new();
    private readonly Dictionary<string, RoomState> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _roomLifetime;

    public InMemoryScrumPokerStore(TimeSpan? roomLifetime = null)
    {
        _roomLifetime = roomLifetime ?? DefaultRoomLifetime;
    }

    public Result<ScrumPokerSession> CreateRoom(string displayName, DateTime utcNow)
    {
        var nameResult = ValidateDisplayName(displayName);
        if (!nameResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(nameResult.Error!, nameResult);

        RoomState state;
        lock (_roomsLock)
        {
            string roomCode;
            do
            {
                roomCode = RandomString(RoomCodeAlphabet, RoomCodeLength);
            } while (_rooms.ContainsKey(roomCode));

            var participant = CreateParticipant(nameResult.Value!, utcNow);
            state = new RoomState(roomCode, utcNow, participant);
            _rooms.Add(roomCode, state);
        }

        return Result<ScrumPokerSession>.Success(CreateSession(state));
    }

    public Result<ScrumPokerSession> JoinRoom(string roomCode, string displayName, DateTime utcNow)
    {
        var nameResult = ValidateDisplayName(displayName);
        if (!nameResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(nameResult.Error!, nameResult);

        var state = FindActiveRoom(roomCode, utcNow);
        if (state is null)
            return Result<ScrumPokerSession>.Failure("Room was not found or has expired.", ErrorType.NotFound);

        lock (state.Gate)
        {
            if (state.Participants.Values.Any(p => string.Equals(p.DisplayName, nameResult.Value, StringComparison.OrdinalIgnoreCase)))
                return Result<ScrumPokerSession>.Failure("That display name is already in use in this room.", ErrorType.Conflict);

            if (state.Participants.Count >= MaxParticipantsPerRoom)
                return Result<ScrumPokerSession>.Failure("The room is full.", ErrorType.Conflict);

            var participant = CreateParticipant(nameResult.Value!, utcNow);
            state.Participants.Add(participant.Id, participant);
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerSession>.Success(CreateSession(state, participant));
        }
    }

    public Result<ScrumPokerRoom> GetRoom(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> SelectCard(string roomCode, string participantToken, ScrumPokerCard? card, DateTime utcNow)
    {
        if (card is not null && !Enum.IsDefined(card.Value))
            return Result<ScrumPokerRoom>.Failure("The selected card is not valid.");

        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            if (state.Phase == ScrumPokerPhase.Revealed)
                return Result<ScrumPokerRoom>.Failure("The cards have already been revealed. Reset the room to vote again.", ErrorType.Conflict);

            state.Participants[participant.Id] = participant with { SelectedCard = card, LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Reveal(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            if (state.Phase == ScrumPokerPhase.Voting)
            {
                state.Phase = ScrumPokerPhase.Revealed;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Hide(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            if (state.Phase == ScrumPokerPhase.Revealed)
            {
                state.Phase = ScrumPokerPhase.Voting;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Reset(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            foreach (var id in state.Participants.Keys.ToArray())
            {
                var current = state.Participants[id];
                state.Participants[id] = current with { SelectedCard = null, LastSeenUtc = id == participant.Id ? utcNow : current.LastSeenUtc };
            }

            state.Phase = ScrumPokerPhase.Voting;
            state.Round++;
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    private Result<string> ValidateDisplayName(string displayName)
    {
        var normalized = displayName?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? Result<string>.Failure("A display name is required.")
            : normalized.Length > MaxDisplayNameLength
                ? Result<string>.Failure($"Display names must be {MaxDisplayNameLength} characters or fewer.")
                : Result<string>.Success(normalized);
    }

    private RoomState? FindActiveRoom(string roomCode, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return null;

        lock (_roomsLock)
        {
            if (!_rooms.TryGetValue(roomCode.Trim(), out var state))
                return null;

            lock (state.Gate)
            {
                if (!IsExpired(state, utcNow))
                    return state;
            }

            _rooms.Remove(state.RoomCode);
            return null;
        }
    }

    private Result<(RoomState State, ScrumPokerParticipant Participant)> GetParticipantRoom(string roomCode, string token, DateTime utcNow)
    {
        var state = FindActiveRoom(roomCode, utcNow);
        if (state is null)
            return Result<(RoomState, ScrumPokerParticipant)>.Failure("Room was not found or has expired.", ErrorType.NotFound);

        lock (state.Gate)
        {
            var participant = state.Participants.Values.FirstOrDefault(p => FixedTimeEquals(p.Token, token));
            return participant is null
                ? Result<(RoomState, ScrumPokerParticipant)>.Failure("The participant token is invalid.", ErrorType.Unauthorized)
                : Result<(RoomState, ScrumPokerParticipant)>.Success((state, participant));
        }
    }

    private bool IsExpired(RoomState state, DateTime utcNow) => utcNow - state.LastActivityUtc >= _roomLifetime;

    private static ScrumPokerParticipant CreateParticipant(string displayName, DateTime utcNow) =>
        new(Guid.NewGuid(), displayName, RandomString("", ParticipantTokenLength), null, utcNow);

    private static ScrumPokerSession CreateSession(RoomState state, ScrumPokerParticipant? participant = null) =>
        new(CreateRoomSnapshot(state), participant ?? state.Participants.Values.First());

    private static ScrumPokerRoom CreateRoomSnapshot(RoomState state) =>
        new(state.RoomCode, state.Round, state.Phase, state.Revision, state.CreatedUtc, state.LastActivityUtc, state.Participants.Values.ToArray());

    private static string RandomString(string alphabet, int length)
    {
        if (string.IsNullOrEmpty(alphabet))
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        var result = new StringBuilder(length);
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        foreach (var value in buffer)
            result.Append(alphabet[value % alphabet.Length]);
        return result.ToString();
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private sealed class RoomState(string roomCode, DateTime createdUtc, ScrumPokerParticipant owner)
    {
        public object Gate { get; } = new();
        public string RoomCode { get; } = roomCode;
        public DateTime CreatedUtc { get; } = createdUtc;
        public DateTime LastActivityUtc { get; set; } = createdUtc;
        public int Round { get; set; } = 1;
        public ScrumPokerPhase Phase { get; set; } = ScrumPokerPhase.Voting;
        public long Revision { get; set; } = 1;
        public Dictionary<Guid, ScrumPokerParticipant> Participants { get; } = new() { [owner.Id] = owner };
    }
}
