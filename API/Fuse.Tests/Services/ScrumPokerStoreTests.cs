using Fuse.Core.Areas.ScrumPoker;
using Fuse.Core.Helpers;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class ScrumPokerStoreTests
{
    private static readonly DateTime Start = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateRoom_ReturnsRoomAndPrivateParticipantToken()
    {
        var store = new InMemoryScrumPokerStore();

        var result = store.CreateRoom(" Alice ", Start);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice", result.Value!.Participant.DisplayName);
        Assert.Equal(result.Value.Participant.Id, Assert.Single(result.Value.Room.Participants).Id);
        Assert.NotEmpty(result.Value.Participant.Token);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Room.Phase);
        Assert.Equal(1, result.Value.Room.Round);
    }

    [Fact]
    public void JoinRoom_RejectsDuplicateNamesIgnoringCase()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;

        var result = store.JoinRoom(owner.Room.RoomCode, " alice ", Start.AddSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public void SelectCard_OnlyChangesTheParticipantWhoOwnsTheToken()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var result = store.SelectCard(owner.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        var alice = result.Value!.Participants.Single(p => p.DisplayName == "Alice");
        var bob = result.Value.Participants.Single(p => p.DisplayName == "Bob");
        Assert.Null(alice.SelectedCard);
        Assert.Equal(ScrumPokerCard.Eight, bob.SelectedCard);
    }

    [Fact]
    public void SelectCard_CanClearAnExistingVote()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(1));

        var result = store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, null, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Participants.Single().SelectedCard);
    }

    [Fact]
    public void GetRoom_RetainsCardsForThePublicProjectionToRedact()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(1));

        var result = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(ScrumPokerCard.Five, result.Value!.Participants.Single().SelectedCard);
        // The store retains the value; the API projection will redact it before reveal.
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Phase);
    }

    [Fact]
    public void Reveal_IsAvailableToAnyParticipantAndIsIdempotent()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var first = store.Reveal(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(2));
        var second = store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(3));

        Assert.Equal(ScrumPokerPhase.Revealed, first.Value!.Phase);
        Assert.Equal(first.Value.Revision, second.Value!.Revision);
    }

    [Fact]
    public void Hide_ReturnsTheRoomToVotingWithoutClearingSelections()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(1));
        store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(2));

        var result = store.Hide(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value!.Phase);
        Assert.Equal(ScrumPokerCard.Five, result.Value.Participants.Single().SelectedCard);
    }

    [Fact]
    public void Reset_ClearsCardsAndStartsANewRoundForEveryone()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(2));
        store.SelectCard(owner.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(3));
        store.Reveal(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(4));

        var result = store.Reset(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Round);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Phase);
        Assert.All(result.Value.Participants, participant => Assert.Null(participant.SelectedCard));
    }

    [Fact]
    public void ExpiredRoomsAreNotJoinableOrReadable()
    {
        var store = new InMemoryScrumPokerStore(TimeSpan.FromMinutes(10));
        var owner = store.CreateRoom("Alice", Start).Value!;

        var join = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddMinutes(10));
        var read = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, Start.AddMinutes(10));

        Assert.Equal(ErrorType.NotFound, join.ErrorType);
        Assert.Equal(ErrorType.NotFound, read.ErrorType);
    }
}
