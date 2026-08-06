<template>
  <div class="page-container scrum-poker-page">
    <div class="page-header">
      <div>
        <h1>Scrum Poker</h1>
        <p class="subtitle">Estimate together without exposing votes before the reveal.</p>
      </div>
    </div>

    <q-banner v-if="!featureEnabled" rounded class="bg-grey-3 text-grey-9 q-mb-lg">
      Scrum Poker is currently disabled in Fuse Settings.
    </q-banner>

    <q-card v-else-if="!session" class="content-card join-card">
      <q-card-section>
        <div class="text-h6">Create or join a room</div>
        <div class="text-caption text-grey-6 q-mt-xs">Choose a display name, then share the room code with your team.</div>
      </q-card-section>
      <q-card-section>
        <q-input v-model="displayName" outlined label="Your display name" maxlength="50" counter @keyup.enter="createRoom" />
        <q-input v-model="joinCode" outlined label="Room code (to join an existing room)" class="q-mt-md" maxlength="20" @keyup.enter="joinRoom" />
        <q-banner v-if="errorMessage" rounded dense class="bg-red-1 text-negative q-mt-md">{{ errorMessage }}</q-banner>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat label="Join room" :disable="!canSubmit || !joinCode" :loading="loading" @click="joinRoom" />
        <q-btn color="primary" label="Create room" :disable="!canSubmit" :loading="loading" @click="createRoom" />
      </q-card-actions>
    </q-card>

    <template v-else>
      <q-banner rounded class="room-banner q-mb-lg">
        <div class="row items-center justify-between no-wrap">
          <div>
            <div class="text-caption">Room code</div>
            <div class="text-h5 text-weight-bold letter-spaced">{{ session.roomCode }}</div>
          </div>
          <q-btn flat icon="content_copy" label="Copy code" @click="copyRoomCode" />
        </div>
      </q-banner>

      <q-banner v-if="errorMessage" rounded dense class="bg-red-1 text-negative q-mb-lg">{{ errorMessage }}</q-banner>

      <div class="row q-col-gutter-lg">
        <div class="col-12 col-md-7">
          <q-card class="content-card">
            <q-card-section class="row items-center justify-between">
              <div>
                <div class="text-h6">Round {{ room?.round ?? 1 }}</div>
                <div class="text-caption text-grey-6">{{ room?.phase === ScrumPokerPhase.Revealed ? 'Cards revealed' : 'Voting in progress' }}</div>
              </div>
              <q-badge :color="room?.phase === ScrumPokerPhase.Revealed ? 'positive' : 'primary'" :label="room?.phase ?? 'Voting'" />
            </q-card-section>
            <q-separator />
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
            <q-card-actions align="right">
              <q-btn flat label="Reset round" icon="restart_alt" :loading="actionLoading" @click="resetRound" />
              <q-btn color="primary" :label="room?.phase === ScrumPokerPhase.Revealed ? 'Hide cards' : 'Reveal cards'" :icon="room?.phase === ScrumPokerPhase.Revealed ? 'visibility_off' : 'visibility'" :loading="actionLoading" @click="room?.phase === ScrumPokerPhase.Revealed ? hideCards() : revealCards()" />
            </q-card-actions>
          </q-card>
        </div>

        <div class="col-12 col-md-5">
          <q-card class="content-card">
            <q-card-section>
              <div class="text-h6">Participants</div>
              <div class="text-caption text-grey-6">{{ room?.participants?.length ?? 0 }} in this room</div>
            </q-card-section>
            <q-separator />
            <q-list separator>
              <q-item v-for="participant in room?.participants ?? []" :key="participant.id">
                <q-item-section avatar><q-avatar color="primary" text-color="white">{{ participant.displayName?.charAt(0).toUpperCase() }}</q-avatar></q-item-section>
                <q-item-section>
                  <q-item-label>{{ participant.displayName }}<q-badge v-if="participant.id === currentParticipantId" outline color="primary" label="You" class="q-ml-sm" /></q-item-label>
                  <q-item-label caption>{{ participant.hasVoted ? 'Card selected' : 'Waiting for a card' }}</q-item-label>
                </q-item-section>
                <q-item-section side><span v-if="participant.card !== undefined && participant.card !== null" class="text-h6">{{ cardLabel(participant.card) }}</span><q-icon v-else-if="participant.hasVoted" name="lock" color="grey-6" /></q-item-section>
              </q-item>
            </q-list>
          </q-card>
          <q-btn flat color="grey-7" label="Leave room" class="q-mt-md" @click="leaveRoom" />
        </div>
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
.join-card { max-width: 620px; }
.room-banner { background: linear-gradient(135deg, var(--q-primary), #6a4bbc); color: white; }
.letter-spaced { letter-spacing: .18em; }
.card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(72px, 1fr)); gap: .75rem; }
.poker-card { min-height: 86px; border: 1px solid rgba(80, 60, 150, .25); font-size: 1.2rem; }
.poker-card--selected { background: var(--q-primary); color: white; transform: translateY(-3px); }
</style>
