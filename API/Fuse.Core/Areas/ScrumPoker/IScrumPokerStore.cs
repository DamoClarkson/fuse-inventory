using Fuse.Core.Helpers;

namespace Fuse.Core.Areas.ScrumPoker;

public interface IScrumPokerStore
{
    Result<ScrumPokerSession> CreateRoom(string displayName, DateTime utcNow);
    Result<ScrumPokerSession> JoinRoom(string roomCode, string displayName, DateTime utcNow);
    Result<ScrumPokerRoom> GetRoom(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> SelectCard(string roomCode, string participantToken, ScrumPokerCard? card, DateTime utcNow);
    Result<ScrumPokerRoom> Reveal(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Hide(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Reset(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Leave(string roomCode, string participantToken, DateTime utcNow);
}
