# Scrum Poker Plan

Status: Phase 2 in progress — backend room API implemented, frontend room experience not started

This document is the working plan for the Scrum Poker feature. Update the checkboxes and decisions here as implementation progresses.

## Product definition

Fuse will provide a lightweight Scrum Poker session where participants join a temporary room with a display name, select a card, and see the group’s votes revealed together.

The feature is deliberately separate from inventory data. Rooms are temporary collaboration state, not inventory entities.

## Agreed decisions

- [x] Use the standard Scrum Poker card set: `0`, `½`, `1`, `2`, `3`, `5`, `8`, `13`, `20`, `40`, `100`, `?`, `Coffee`.
- [x] Any participant may reveal the cards.
- [x] Revealed cards can be hidden again without clearing the current selections.
- [x] Any participant may reset the round.
- [x] Rooms do not need to survive an application restart.
- [x] The feature is controlled by a Fuse App Settings toggle.
- [x] The feature is disabled by default.
- [x] Rooms are stored in memory, separately from the inventory `Snapshot`.
- [x] Room state is synchronised initially by approximately one-second polling.

## Proposed defaults

- Maximum participants per room: 20
- Room inactivity expiry: 4 hours
- Username uniqueness: case-insensitive within a room
- Room access: random opaque room code
- Participant access: random private participant token
- Reveal/reset permissions: any participant in the room

These defaults can be adjusted during implementation if testing exposes a better choice.

## Architecture

### Backend

- Add `ScrumPokerEnabled` to `AppSettings`, defaulting to `false`.
- Add the toggle to the existing Settings page and reuse the existing App Settings update permission.
- Create a separate Scrum Poker domain area in `Fuse.Core`.
- Add an in-memory singleton store in `Fuse.Data` or the appropriate persistence layer.
- Use per-room synchronisation so joining, voting, revealing, and resetting are atomic.
- Add automatic cleanup of inactive rooms.
- Do not add rooms to `Snapshot`, inventory exports, audit history, or version history.

### API surface

The exact route names may be refined to match generated client conventions, but the intended operations are:

```text
POST /api/scrum-poker/rooms
POST /api/scrum-poker/rooms/{roomCode}/join
GET  /api/scrum-poker/rooms/{roomCode}/state
PUT  /api/scrum-poker/rooms/{roomCode}/participants/{participantToken}/card
POST /api/scrum-poker/rooms/{roomCode}/reveal
POST /api/scrum-poker/rooms/{roomCode}/reset
```

The API must:

- Reject room operations when Scrum Poker is disabled.
- Validate room codes and participant tokens without exposing sensitive state.
- Enforce non-empty, length-limited usernames.
- Reject duplicate usernames within a room, ignoring case.
- Only allow a participant to change their own card.
- Accept only cards from the standard deck.
- Hide card values until the room is revealed.
- Return a consistent room revision/round so clients can detect state changes.

### Room state

A room should contain:

- Room code and creation/last-activity timestamps
- Current round number
- Voting state: `Voting` or `Revealed`
- Revision number
- Participants with IDs, display names, private tokens, selected cards, and last-seen timestamps

While voting, the public state should expose whether each participant has voted, but not the card value. After reveal, all selected cards become visible to every participant. Reset clears all selections, returns the room to `Voting`, increments the round, and increments the revision.

### Frontend

- Add public routes such as `/scrum-poker` and `/scrum-poker/:roomCode`.
- Add a create/join screen with room code and display name inputs.
- Add a room screen with participant status, card selection, reveal, reset, room-code sharing, and expiry/error states.
- Poll room state at roughly one-second intervals while the room is active.
- Stop or slow polling when the room expires or the feature becomes disabled.
- Hide the navigation entry and block direct access when `ScrumPokerEnabled` is false.
- Follow the existing Vue, Quasar, TanStack Query, generated API client, and composable patterns.

## Implementation phases

### Phase 1 — Settings and contracts

- [x] Add `ScrumPokerEnabled` to the `AppSettings` model with a safe default of `false`.
- [x] Validate and round-trip the new setting through the existing settings API.
- [x] Add the Settings page toggle and generated frontend client typing.
- [x] Define room, participant, card, and public-state contracts.
- [ ] Define feature-disabled, room-not-found, room-expired, duplicate-name, and invalid-token errors.

### Phase 2 — Backend room service

- [x] Implement the in-memory room store.
- [x] Implement random room-code and participant-token generation.
- [x] Implement room creation and joining.
- [x] Implement card selection.
- [x] Implement reveal and reset.
- [x] Implement room revision and round handling.
- [x] Implement inactivity expiry and cleanup.
- [x] Enforce the feature toggle at the service/API boundary.

### Phase 3 — Backend tests

- [x] Test room creation and joining.
- [x] Test duplicate-name rejection.
- [x] Test participant-token ownership.
- [x] Test valid and invalid card selections.
- [x] Test card state before reveal (public redaction remains part of the API slice).
- [x] Test shared reveal state.
- [x] Test hiding revealed cards while retaining selections.
- [x] Test reset behaviour.
- [ ] Test concurrent votes and reveal/reset operations.
- [x] Test expiry and feature-disabled behaviour.

### Phase 4 — Frontend experience

- [x] Add API client methods and a Scrum Poker page integration.
- [x] Add create/join page.
- [x] Add room page and participant list.
- [x] Add card-selection controls.
- [x] Add reveal/reset controls.
- [x] Add polling and revision-aware state updates.
- [x] Add copy-room-code affordance.
- [x] Add loading, invalid-room, expired-room, and disabled-feature states.
- [x] Hide or show navigation based on the setting.

### Phase 5 — End-to-end verification

- [ ] Build the backend and frontend.
- [ ] Run the existing unit test suite.
- [ ] Run the existing frontend checks.
- [ ] Test two browser sessions joining one room.
- [ ] Verify that votes remain hidden in both sessions until reveal.
- [ ] Verify reveal propagates to both sessions.
- [ ] Verify reset starts a new round in both sessions.
- [ ] Verify restart loses rooms as intended.
- [ ] Verify disabling the feature blocks new and existing room operations.
- [ ] Update user-facing documentation.

## Acceptance criteria

The feature is ready when:

1. An authorised Fuse administrator can enable Scrum Poker from Settings.
2. A user can create a room and receive a shareable room code.
3. Multiple users can join with unique display names without Fuse accounts.
4. Each user can select and change their own card before reveal.
5. Participants can see who has voted without seeing hidden card values.
6. Any participant can reveal the room and everyone sees the same revealed state.
7. Any participant can reset the room for another round.
8. Invalid, expired, disabled, and unauthorised operations fail safely.
9. Rooms are not persisted into inventory data and disappear after restart.
10. The feature is covered by backend tests and a two-session end-to-end test.

## Open questions to resolve during implementation

- [ ] Confirm exact maximum room size and expiry duration through UI/testing feedback.
- [ ] Decide whether the room creator needs a visible host indicator; no special host permissions are currently planned.
- [x] Participants can leave explicitly; the server removes them immediately while retaining an empty room until normal expiry.
- [ ] Decide whether cards should be configurable in a later version; the first version uses the fixed standard deck.

## Progress log

| Date | Update |
| --- | --- |
| 2026-08-06 | Planning completed; implementation not started. |
| 2026-08-06 | Added the default-off `ScrumPokerEnabled` setting, generated client fields, and Settings-page toggle. Frontend type-check passed; full build is blocked by a missing Rollup optional native dependency, and backend verification is blocked because the .NET SDK is not installed. |
| 2026-08-06 | Added the in-memory room store, standard card/room models, token generation, atomic room operations, expiry, DI registration, and focused unit tests. Non-integration suite passes: 567 tests, 0 failures. |
| 2026-08-06 | Added the anonymous Scrum Poker API, App Settings enforcement, participant-token validation, and safe public-state projection. Added controller tests for disabled access and card redaction. Non-integration suite passes: 569 tests, 0 failures. |
| 2026-08-06 | Added the checked-in frontend API client methods, create/join and room routes, polling room UI, standard card controls, reveal/reset actions, room-code copying, and feature-gated navigation. Vue type-check and production build pass. |
| 2026-08-06 | Added card deselection, reversible reveal/hide behaviour, and moved the Scrum Poker navigation item into Integrations. Backend suite passes 571 tests; frontend production build passes. |
| 2026-08-06 | Added a real leave-room API operation so participants are removed server-side and can rejoin with the same display name. Backend suite passes 572 tests; frontend production build passes. |
