<template>
  <div class="scrum-poker-page">
    <header class="scrum-header">
      <div class="brand-lockup">
        <div class="brand-mark"><q-icon name="style" size="22px" /></div>
        <div>
          <div class="eyebrow">Fuse workspace</div>
          <h1>Scrum Poker</h1>
        </div>
      </div>
      <div v-if="session" class="room-pill">
        <span class="room-pill__label">ROOM</span>
        <strong>{{ session.roomCode }}</strong>
        <q-btn flat round dense icon="content_copy" aria-label="Copy room code" @click="copyRoomCode">
          <q-tooltip>Copy room code</q-tooltip>
        </q-btn>
      </div>
    </header>

    <q-banner v-if="!featureEnabled" rounded class="bg-grey-3 text-grey-9 q-mb-lg">
      Scrum Poker is currently disabled in Fuse Settings.
    </q-banner>

    <q-card v-else-if="!session" flat bordered class="join-card">
      <q-card-section class="join-card__intro">
        <div class="welcome-icon"><q-icon name="groups" size="30px" /></div>
        <div>
          <div class="section-kicker">Collaborative estimation</div>
          <div class="join-title">Create or join a room</div>
          <div class="join-copy">Choose a display name, then share the room with your team.</div>
        </div>
      </q-card-section>
      <q-card-section class="join-form">
        <q-input v-model="displayName" outlined label="Your display name" maxlength="50" counter @keyup.enter="roomCodeFromUrl ? enterRoom() : createRoom()" />
        <q-banner v-if="roomCodeFromUrl" rounded dense class="room-notice">You’re entering room <strong>{{ roomCodeFromUrl }}</strong>.</q-banner>
        <q-input v-else v-model="joinCode" outlined label="Room code (optional)" class="q-mt-md" maxlength="20" @keyup.enter="joinRoom" />
        <q-banner v-if="errorMessage" rounded dense class="bg-red-1 text-negative q-mt-md">{{ errorMessage }}</q-banner>
      </q-card-section>
      <q-card-actions class="join-actions">
        <q-btn v-if="roomCodeFromUrl" unelevated color="primary" label="Enter room" :disable="!canSubmit" :loading="loading" @click="enterRoom" />
        <template v-else>
          <q-btn flat color="grey-8" label="Join existing room" :disable="!canSubmit || !joinCode" :loading="loading" @click="joinRoom" />
          <q-btn unelevated color="primary" label="Create new room" :disable="!canSubmit" :loading="loading" @click="createRoom" />
        </template>
      </q-card-actions>
    </q-card>

    <template v-else>
      <q-banner v-if="errorMessage" rounded dense class="bg-red-1 text-negative q-mb-lg">{{ errorMessage }}</q-banner>

      <div class="scrum-layout">
        <main class="voting-panel">
          <div class="round-heading">
            <div>
              <div class="section-kicker">Current round</div>
              <h2>Round {{ room?.round ?? 1 }}</h2>
              <p>{{ room?.phase === ScrumPokerPhase.Revealed ? 'The team has revealed their cards.' : 'Choose a card. Votes stay hidden until everyone is ready.' }}</p>
            </div>
            <q-chip dense :color="room?.phase === ScrumPokerPhase.Revealed ? 'positive' : 'primary'" text-color="white" :icon="room?.phase === ScrumPokerPhase.Revealed ? 'check' : 'schedule'" :label="room?.phase === ScrumPokerPhase.Revealed ? 'Revealed' : 'Voting'" />
          </div>
          <q-card flat bordered class="cards-card">
            <q-card-section>
              <div class="card-grid">
                <q-btn
                  v-for="card in cards"
                  :key="card.value"
                  class="poker-card"
                  :class="{ 'poker-card--selected': selectedCard === card.value }"
                  :label="card.label"
                  :disable="room?.phase === ScrumPokerPhase.Revealed || actionLoading"
                  @click="selectCard(selectedCard === card.value ? null : card.value)"
                />
              </div>
            </q-card-section>
            <q-card-actions class="voting-actions">
              <q-btn flat color="grey-7" label="Reset" icon="restart_alt" :loading="actionLoading" @click="resetRound" />
              <q-btn unelevated color="primary" :label="room?.phase === ScrumPokerPhase.Revealed ? 'Hide cards' : 'Reveal cards'" :icon="room?.phase === ScrumPokerPhase.Revealed ? 'visibility_off' : 'visibility'" :loading="actionLoading" @click="room?.phase === ScrumPokerPhase.Revealed ? hideCards() : revealCards()" />
            </q-card-actions>
          </q-card>
          <div v-if="room?.phase === ScrumPokerPhase.Revealed && room.average !== undefined && room.average !== null" class="average-card">
            <div class="average-card__label">Team average</div>
            <div class="average-card__value">{{ room.average }}</div>
            <div class="average-card__hint">Based on the revealed estimates</div>
          </div>
        </main>

        <aside class="participants-panel">
          <div class="panel-heading">
            <div>
              <div class="section-kicker">In this room</div>
              <h3>Participants</h3>
            </div>
            <span class="participant-count">{{ room?.participants?.length ?? 0 }}</span>
          </div>
          <q-list class="participant-list">
              <q-item v-for="participant in room?.participants ?? []" :key="participant.id">
                <q-item-section avatar><q-avatar class="participant-avatar">{{ participant.displayName?.charAt(0).toUpperCase() }}</q-avatar></q-item-section>
                <q-item-section>
                  <q-item-label>{{ participant.displayName }}<q-badge v-if="participant.id === currentParticipantId" outline color="primary" label="You" class="q-ml-sm" /></q-item-label>
                  <q-item-label caption><span class="status-dot" :class="{ 'status-dot--ready': participant.hasVoted }"></span>{{ participant.hasVoted ? 'Ready' : 'Choosing a card' }}</q-item-label>
                </q-item-section>
                <q-item-section side><span v-if="participant.card !== undefined && participant.card !== null" class="participant-card">{{ cardLabel(participant.card) }}</span><q-icon v-else-if="participant.hasVoted" name="lock" color="grey-6" /></q-item-section>
              </q-item>
            </q-list>
          <q-btn flat color="grey-7" label="Leave room" icon="logout" class="leave-button" @click="leaveRoom" />
        </aside>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Notify } from 'quasar'
import { ScrumPokerCard, ScrumPokerPhase, ScrumPokerRoomResponse, ScrumPokerSessionResponse } from 'api/client'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'

const route = useRoute()
const router = useRouter()
const fuseStore = useFuseStore()
const client = useFuseClient()
const displayName = ref('')
const joinCode = ref('')
const session = ref<ScrumPokerSessionResponse | null>(null)
const room = ref<ScrumPokerRoomResponse | null>(null)
const participantName = ref('')
const loading = ref(false)
const actionLoading = ref(false)
const errorMessage = ref('')
const selectedCard = ref<ScrumPokerCard | null>(null)
let pollTimer: ReturnType<typeof setInterval> | undefined

const featureEnabled = computed(() => fuseStore.appSettings?.scrumPokerEnabled === true)
const roomCodeFromUrl = computed(() => typeof route.params.roomCode === 'string' ? route.params.roomCode.toUpperCase() : '')
const canSubmit = computed(() => displayName.value.trim().length > 0 && displayName.value.trim().length <= 50)
const currentParticipantId = computed(() => room.value?.participants?.find(p => p.displayName === participantName.value)?.id)
const cards = [
  { value: ScrumPokerCard.Zero, label: '0' }, { value: ScrumPokerCard.Half, label: '½' }, { value: ScrumPokerCard.One, label: '1' },
  { value: ScrumPokerCard.Two, label: '2' }, { value: ScrumPokerCard.Three, label: '3' }, { value: ScrumPokerCard.Five, label: '5' },
  { value: ScrumPokerCard.Eight, label: '8' }, { value: ScrumPokerCard.Thirteen, label: '13' }, { value: ScrumPokerCard.Twenty, label: '20' },
  { value: ScrumPokerCard.Forty, label: '40' }, { value: ScrumPokerCard.Hundred, label: '100' }, { value: ScrumPokerCard.Question, label: '?' },
  { value: ScrumPokerCard.Coffee, label: '☕' }
]

function cardLabel(card: ScrumPokerCard) { return cards.find(option => option.value === card)?.label ?? card }
function storageKey(code: string) { return `fuse:scrum-poker:${code}` }

async function createRoom() {
  if (!canSubmit.value) return
  await runSessionAction(() => client.scrumPokerRoomsPOST({ displayName: displayName.value.trim() } as any))
}

async function joinRoom() {
  if (!canSubmit.value || !joinCode.value.trim()) return
  const code = joinCode.value.trim().toUpperCase()
  await runSessionAction(() => client.scrumPokerRoomsJoin(code, { displayName: displayName.value.trim() } as any))
}

async function enterRoom() {
  if (!canSubmit.value || !roomCodeFromUrl.value) return
  await runSessionAction(() => client.scrumPokerRoomsEnter(roomCodeFromUrl.value, { displayName: displayName.value.trim() } as any))
}

async function runSessionAction(action: () => Promise<ScrumPokerSessionResponse>) {
  loading.value = true
  errorMessage.value = ''
  try {
    const result = await action()
    session.value = result
    room.value = result.room ?? null
    participantName.value = displayName.value.trim()
    selectedCard.value = currentCard()
    if (result.roomCode && result.participantToken) sessionStorage.setItem(storageKey(result.roomCode), JSON.stringify({ session: result, participantName: participantName.value }))
    await router.replace({ name: 'scrumPokerRoom', params: { roomCode: result.roomCode } })
    startPolling()
  } catch (error) { errorMessage.value = error instanceof Error ? error.message : 'Unable to join the room.' }
  finally { loading.value = false }
}

function currentCard() {
  const me = session.value?.room?.participants?.find(p => p.displayName === participantName.value)
  return me?.card ?? null
}

async function refreshRoom() {
  if (!session.value?.roomCode || !session.value.participantToken) return
  try {
    const result = await client.scrumPokerState(session.value.roomCode, session.value.participantToken)
    room.value = result
    const me = result.participants?.find(p => p.id === currentParticipantId.value)
    selectedCard.value = me?.card ?? selectedCard.value
  } catch (error) { errorMessage.value = error instanceof Error ? error.message : 'The room is no longer available.'; stopPolling() }
}

function startPolling() { stopPolling(); void refreshRoom(); pollTimer = setInterval(() => void refreshRoom(), 1000) }
function stopPolling() { if (pollTimer) { clearInterval(pollTimer); pollTimer = undefined } }

async function selectCard(card: ScrumPokerCard | null) {
  if (!session.value) return
  actionLoading.value = true; errorMessage.value = ''
  try { room.value = await client.scrumPokerCardPUT(session.value.roomCode!, { participantToken: session.value.participantToken, card } as any); selectedCard.value = card }
  catch (error) { errorMessage.value = error instanceof Error ? error.message : 'Unable to select that card.' }
  finally { actionLoading.value = false }
}
async function revealCards() { await roomAction(() => client.scrumPokerReveal(session.value!.roomCode!, { participantToken: session.value!.participantToken } as any)) }
async function hideCards() { await roomAction(() => client.scrumPokerHide(session.value!.roomCode!, { participantToken: session.value!.participantToken } as any)) }
async function resetRound() { await roomAction(() => client.scrumPokerReset(session.value!.roomCode!, { participantToken: session.value!.participantToken } as any)) ; selectedCard.value = null }
async function roomAction(action: () => Promise<ScrumPokerRoomResponse>) { actionLoading.value = true; errorMessage.value = ''; try { room.value = await action() } catch (error) { errorMessage.value = error instanceof Error ? error.message : 'Unable to update the room.' } finally { actionLoading.value = false } }
async function copyRoomCode() { if (session.value?.roomCode) { await navigator.clipboard?.writeText(session.value.roomCode); Notify.create({ message: 'Room code copied', color: 'positive' }) } }
async function leaveRoom() {
  const currentSession = session.value
  stopPolling()
  if (currentSession?.roomCode && currentSession.participantToken) {
    try { await client.scrumPokerLeave(currentSession.roomCode, { participantToken: currentSession.participantToken } as any) }
    catch { /* The local session should still be cleared if the room has already expired. */ }
    sessionStorage.removeItem(storageKey(currentSession.roomCode))
  }
  session.value = null; room.value = null; selectedCard.value = null; participantName.value = ''
  await router.replace({ name: 'scrumPoker' })
}

async function loadStoredSession() {
  if (!featureEnabled.value) return
  const code = route.params.roomCode as string | undefined
  if (!code) return
  const stored = sessionStorage.getItem(storageKey(code))
  if (!stored) { joinCode.value = code; return }
  try {
    const saved = JSON.parse(stored)
    session.value = saved.session ?? saved
    participantName.value = saved.participantName ?? ''
    displayName.value = participantName.value
    room.value = session.value?.room ?? null
    startPolling()
  }
  catch { sessionStorage.removeItem(storageKey(code)) }
}
onMounted(async () => { await fuseStore.fetchStatus(); await loadStoredSession() })
watch(() => route.params.roomCode, () => { if (!session.value) void loadStoredSession() })
onBeforeUnmount(stopPolling)
</script>

<style scoped>
@import '../styles/pages.css';
.scrum-poker-page { min-height: 100%; padding: 2.25rem clamp(1rem, 4vw, 4rem); background: #f7f8fc; color: #202437; }
.scrum-header { display: flex; justify-content: space-between; align-items: center; max-width: 1180px; margin: 0 auto 2.5rem; }
.brand-lockup { display: flex; align-items: center; gap: .8rem; }
.brand-mark, .welcome-icon { display: grid; place-items: center; color: #fff; background: #6557d9; border-radius: 13px; box-shadow: 0 8px 18px rgba(101, 87, 217, .2); }
.brand-mark { width: 44px; height: 44px; }
.eyebrow, .section-kicker { color: #85899c; font-size: .7rem; font-weight: 700; letter-spacing: .1em; text-transform: uppercase; }
.scrum-header h1 { margin: .1rem 0 0; font-size: 1.45rem; font-weight: 700; }
.room-pill { display: flex; align-items: center; gap: .55rem; padding: .25rem .35rem .25rem .8rem; border: 1px solid #e2e3ec; border-radius: 999px; background: #fff; color: #4b4e61; font-size: .9rem; box-shadow: 0 3px 12px rgba(25, 31, 60, .04); }
.room-pill__label { color: #999caf; font-size: .63rem; font-weight: 700; letter-spacing: .1em; }
.join-card { width: min(100%, 620px); margin: 3rem auto; border-color: #e4e5ee; border-radius: 18px; box-shadow: 0 14px 40px rgba(25, 31, 60, .07); }
.join-card__intro { display: flex; align-items: center; gap: 1rem; padding: 2rem 2rem 1rem; }
.welcome-icon { width: 54px; height: 54px; border-radius: 16px; }
.join-title { margin-top: .25rem; font-size: 1.35rem; font-weight: 700; }
.join-copy { margin-top: .3rem; color: #85899c; font-size: .9rem; }
.join-form { padding: 1rem 2rem 1.5rem; }
.join-actions { justify-content: flex-end; gap: .5rem; padding: 0 2rem 1.75rem; }
.room-notice { background: #eeecff; color: #5146bd; }
.scrum-layout { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 1.5rem; max-width: 1180px; margin: 0 auto; }
.round-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
.round-heading h2, .panel-heading h3 { margin: .3rem 0 0; font-size: 1.45rem; font-weight: 700; }
.round-heading p { margin: .4rem 0 0; color: #85899c; font-size: .88rem; }
.cards-card, .participants-panel { border: 1px solid #e4e5ee; border-radius: 16px; background: #fff; box-shadow: 0 8px 24px rgba(25, 31, 60, .045); }
.card-grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: .75rem; }
.poker-card { min-height: 88px; border: 1px solid #e2e3ec; border-radius: 11px; color: #4f5368; background: #fff; font-size: 1.15rem; font-weight: 600; transition: transform .15s ease, box-shadow .15s ease, border-color .15s ease; }
.poker-card:hover { border-color: #aaa2ee; box-shadow: 0 5px 12px rgba(101, 87, 217, .12); transform: translateY(-2px); }
.poker-card--selected { border-color: #6557d9; background: #6557d9; color: white; box-shadow: 0 7px 15px rgba(101, 87, 217, .25); transform: translateY(-4px); }
.voting-actions { justify-content: space-between; padding: 1rem 1.25rem 1.25rem; border-top: 1px solid #f0f0f5; }
.participants-panel { padding: 1.25rem; align-self: start; }
.panel-heading { display: flex; align-items: center; justify-content: space-between; padding: .25rem .25rem 1rem; }
.panel-heading h3 { font-size: 1.1rem; }
.participant-count { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 50%; background: #eeecff; color: #5146bd; font-size: .78rem; font-weight: 700; }
.participant-list { border-top: 1px solid #f0f0f5; }
.participant-list .q-item { min-height: 68px; padding: .65rem .25rem; }
.participant-avatar { background: #e9e7ff; color: #5a4fca; font-weight: 700; }
.status-dot { display: inline-block; width: 6px; height: 6px; margin: 0 .35rem .1rem 0; border-radius: 50%; background: #c8cad4; }
.status-dot--ready { background: #36b37e; }
.participant-card { display: grid; place-items: center; min-width: 30px; height: 34px; border-radius: 7px; background: #eeecff; color: #5146bd; font-weight: 700; }
.leave-button { margin-top: .6rem; }
.average-card { display: flex; align-items: center; gap: 1rem; margin-top: 1rem; padding: 1rem 1.25rem; border: 1px solid #d9f1e6; border-radius: 12px; background: #f1fbf6; }
.average-card__label { color: #458369; font-size: .82rem; font-weight: 600; }
.average-card__value { color: #1d8058; font-size: 1.6rem; font-weight: 800; }
.average-card__hint { color: #76a990; font-size: .75rem; }
@media (max-width: 850px) { .scrum-layout { grid-template-columns: 1fr; } .card-grid { grid-template-columns: repeat(5, 1fr); } }
@media (max-width: 520px) { .scrum-poker-page { padding: 1.25rem .75rem; } .scrum-header { margin-bottom: 1.5rem; } .room-pill__label { display: none; } .card-grid { grid-template-columns: repeat(4, 1fr); gap: .5rem; } .poker-card { min-height: 64px; } .join-card__intro, .join-form { padding-left: 1.25rem; padding-right: 1.25rem; } .join-actions { padding-left: 1.25rem; padding-right: 1.25rem; flex-wrap: wrap; } }
</style>
