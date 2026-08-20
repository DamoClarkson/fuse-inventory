using Fuse.Core.Areas.ScrumPoker;
using Xunit;

namespace Fuse.Tests.Areas.ScrumPoker;

public sealed class InMemoryScrumPokerStoreTests
{
    [Fact]
    public void CreateRoom_CreatorIsOwnerAndHost()
    {
        var store = new InMemoryScrumPokerStore();
        var session = store.CreateRoom("Damian", DateTime.UtcNow).Value!;

        Assert.Equal(session.Participant.Id, session.Room.OwnerParticipantId);
        Assert.Equal(session.Participant.Id, session.Room.CurrentHostParticipantId);
    }

    [Fact]
    public void OwnerLeaves_TemporaryHostFacilitates_AndOwnerReturnsAsHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;
        var john = store.JoinRoom(owner.Room.RoomCode, "John", DateTime.UtcNow.AddSeconds(2)).Value!;

        var afterOwnerLeaves = store.Leave(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(3)).Value!;
        Assert.Equal(sarah.Participant.Id, afterOwnerLeaves.CurrentHostParticipantId);
        Assert.Equal(owner.Participant.Id, afterOwnerLeaves.OwnerParticipantId);
        Assert.Equal(sarah.Participant.Id, store.Reveal(owner.Room.RoomCode, sarah.Participant.Token, DateTime.UtcNow.AddSeconds(4)).Value!.CurrentHostParticipantId);

        var ownerReturns = store.JoinRoom(owner.Room.RoomCode, "Damian", DateTime.UtcNow.AddSeconds(5), owner.Participant.Token).Value!;
        Assert.Equal(owner.Participant.Id, ownerReturns.Participant.Id);
        Assert.Equal(owner.Participant.Id, ownerReturns.Room.CurrentHostParticipantId);
        Assert.Contains(ownerReturns.Room.Participants, participant => participant.Id == john.Participant.Id);
    }

    [Fact]
    public void EmptyRoom_ExpiresAfterFourHoursWithoutActivity()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;
        var empty = store.Leave(owner.Room.RoomCode, owner.Participant.Token, now.AddSeconds(1)).Value!;

        Assert.Empty(empty.Participants);
        Assert.Null(empty.CurrentHostParticipantId);
        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(4).AddSeconds(-1)));
        Assert.False(store.RoomExists(owner.Room.RoomCode, now.AddHours(4).AddSeconds(1)));

        var rejoin = store.JoinRoom(owner.Room.RoomCode, "Sarah", now.AddHours(4).AddSeconds(2));
        Assert.False(rejoin.IsSuccess);
    }

    [Fact]
    public void ActiveParticipantActivityExtendsRoomLifetime()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;

        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(3).AddMinutes(59)));
        Assert.True(store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, now.AddHours(3).AddMinutes(59)).IsSuccess);
        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(7).AddMinutes(58)));
        Assert.False(store.RoomExists(owner.Room.RoomCode, now.AddHours(7).AddMinutes(59).AddSeconds(1)));
    }

    [Fact]
    public void TransferOwnership_RequiresOwner_AndMakesNewOwnerHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;

        var unauthorized = store.TransferOwnership(owner.Room.RoomCode, sarah.Participant.Token, owner.Participant.Id, DateTime.UtcNow.AddSeconds(2));
        Assert.False(unauthorized.IsSuccess);

        var transferred = store.TransferOwnership(owner.Room.RoomCode, owner.Participant.Token, sarah.Participant.Id, DateTime.UtcNow.AddSeconds(3)).Value!;
        Assert.Equal(sarah.Participant.Id, transferred.OwnerParticipantId);
        Assert.Equal(sarah.Participant.Id, transferred.CurrentHostParticipantId);
    }

    [Fact]
    public void Reveal_RequiresCurrentHost_NotPermanentOwner()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;
        store.Leave(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(2));

        Assert.False(store.Reveal(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(3)).IsSuccess);
        Assert.True(store.Reveal(owner.Room.RoomCode, sarah.Participant.Token, DateTime.UtcNow.AddSeconds(4)).IsSuccess);
    }
}