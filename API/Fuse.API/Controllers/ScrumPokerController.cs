using Fuse.Core.Areas.ScrumPoker;
using Fuse.Core.Interfaces;
using Fuse.Core.Helpers;
using Microsoft.AspNetCore.Mvc;
using FuseResult = Fuse.Core.Helpers.IResult;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/scrum-poker")]
public sealed class ScrumPokerController(
    IScrumPokerStore store,
    IFuseStore fuseStore) : ControllerBase
{
    [HttpPost("rooms")]
    [ProducesResponseType<ScrumPokerSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerSessionResponse>> CreateRoom([FromBody] ScrumPokerJoinRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.CreateRoom(request.DisplayName, DateTime.UtcNow);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, ToSessionResponse(result.Value!))
            : ToError<ScrumPokerSessionResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/join")]
    [ProducesResponseType<ScrumPokerSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScrumPokerSessionResponse>> JoinRoom(string roomCode, [FromBody] ScrumPokerJoinRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.JoinRoom(roomCode, request.DisplayName, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToSessionResponse(result.Value!)) : ToError<ScrumPokerSessionResponse>(result);
    }

    [HttpGet("rooms/{roomCode}/state")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> GetState(string roomCode, [FromQuery] string participantToken)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.GetRoom(roomCode, participantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, participantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPut("rooms/{roomCode}/card")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> SelectCard(string roomCode, [FromBody] ScrumPokerCardRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.SelectCard(roomCode, request.ParticipantToken, request.Card, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/reveal")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Reveal(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Reveal(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/reset")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Reset(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Reset(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/hide")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Hide(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Hide(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    private Task<bool> IsEnabled() => fuseStore.GetAsync(snapshot => snapshot.AppSettings.ScrumPokerEnabled);

    private static ScrumPokerSessionResponse ToSessionResponse(ScrumPokerSession session) =>
        new(session.Room.RoomCode, session.Participant.Token, ToRoomResponse(session.Room, session.Participant.Token));

    private static ScrumPokerRoomResponse ToRoomResponse(ScrumPokerRoom room, string participantToken) =>
        new(
            room.RoomCode,
            room.Round,
            room.Phase,
            room.Revision,
            room.CreatedUtc,
            room.LastActivityUtc,
            room.Participants.Select(participant => new ScrumPokerParticipantResponse(
                participant.Id,
                participant.DisplayName,
                participant.SelectedCard is not null,
                room.Phase == ScrumPokerPhase.Revealed || FixedTimeEquals(participant.Token, participantToken)
                    ? participant.SelectedCard
                    : null)).ToArray());

    private static ActionResult<T> ToError<T>(FuseResult result) => result.ErrorType switch
    {
        ErrorType.NotFound => new NotFoundObjectResult(new { error = result.Error }),
        ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = result.Error }),
        ErrorType.Conflict => new ConflictObjectResult(new { error = result.Error }),
        _ => new BadRequestObjectResult(new { error = result.Error })
    };

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

public sealed record ScrumPokerJoinRequest(string DisplayName);

public sealed record ScrumPokerParticipantRequest(string ParticipantToken);

public sealed record ScrumPokerCardRequest(string ParticipantToken, ScrumPokerCard? Card);

public sealed record ScrumPokerSessionResponse(
    string RoomCode,
    string ParticipantToken,
    ScrumPokerRoomResponse Room);

public sealed record ScrumPokerRoomResponse(
    string RoomCode,
    int Round,
    ScrumPokerPhase Phase,
    long Revision,
    DateTime CreatedUtc,
    DateTime LastActivityUtc,
    IReadOnlyList<ScrumPokerParticipantResponse> Participants);

public sealed record ScrumPokerParticipantResponse(
    Guid Id,
    string DisplayName,
    bool HasVoted,
    ScrumPokerCard? Card);
