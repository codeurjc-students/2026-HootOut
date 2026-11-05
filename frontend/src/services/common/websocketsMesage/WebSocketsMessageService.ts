import type { WebSocketService } from '@/services/common/websockets/WebSocketService'
import webSocketService from '@/services/common/websockets/WebSocketService'
import {
  WSMessageType,
  type ChatChannel,
  type WebSocketMessage,
  type WSTicketRespone,
} from '@/types'
import { ref, watch } from 'vue'
import { newId } from '../utils'
import { post } from '../api/ApiService'
import { getConfig } from '@/config'

type Pending = {
  resolve: (m: WebSocketMessage) => void
  reject: (e: Error) => void
  timer: ReturnType<typeof setTimeout>
}

export type AckPromise = {
  id: string
  promise: Promise<WebSocketMessage>
}

export class WebSocketMessageService {
  private webSocketService: WebSocketService = webSocketService
  public connected = ref(false)
  public isActive = false

  private subscriptions: Record<string, (payload: WebSocketMessage) => void> = {}
  private pendingSubscriptions: Record<string, (payload: WebSocketMessage) => void> = {}
  private pending = new Map<string, Pending>()

  async connect(onOnpen: () => Promise<void>) {
    this.isActive = true
    if (this.webSocketService.isConnected()) {
      await onOnpen()
      return
    }

    const { ticket } = await post<WSTicketRespone>('auth/ws-ticket')

    const url = `${getConfig().WSS_URL}?ticket=${ticket}`

    this.webSocketService.connect(url, {
      onOpen: async () => {
        this.connected.value = true
        await onOnpen()
      },
      onClose: () => this.onClose(),
      onError: () => this.onClose(),
      onMessage: (event) => {
        const message = this.parseMessage(event.data)
        if (!message) return
        if (!this.checkAck(message)) {
          this.onMessage(message)
        }
      },
    })
  }

  // Returns null for messages that are not valid JSON, so they are ignored
  private parseMessage(data: string): WebSocketMessage | null {
    try {
      return JSON.parse(data) as WebSocketMessage
    } catch (err) {
      return null
    }
  }

  disconnect() {
    // Not active anymore, so the automatic reconnection does not start again
    this.isActive = false
    this.webSocketService.close()
    // close() does not call onClose for an intentional close, so update the state here
    this.onClose()
  }

  onClose() {
    this.connected.value = false
    // fail everything still waiting, so nothing hangs forever
    for (const [id, p] of this.pending) {
      clearTimeout(p.timer)
      p.reject(new Error('Connection closed'))
      this.pending.delete(id)
    }
  }

  checkAck(message: WebSocketMessage): boolean {
    if (!message.replyTo || !this.pending.has(message.replyTo)) return false

    const p = this.pending.get(message.replyTo)!
    clearTimeout(p.timer)
    this.pending.delete(message.replyTo)

    if (message.type === WSMessageType.Error) {
      p.reject(new Error('Server error'))
    } else {
      p.resolve(message)
    }
    // Acks (including error acks) are answers to our own messages, not channel messages
    return true
  }

  sendWithAck(message: WebSocketMessage, timeoutMs = 5000): AckPromise {
    const id = newId()
    message.id = id

    return {
      id: id,
      promise: new Promise<WebSocketMessage>((resolve, reject) => {
        const timer = setTimeout(() => {
          this.pending.delete(id)
          reject(new Error(`No ack for ${message.type} within ${timeoutMs}ms`))
        }, timeoutMs)

        this.pending.set(id, { resolve, reject, timer })

        try {
          this.webSocketService.send(JSON.stringify(message))
        } catch (err) {
          // The message was never sent, so no ack will arrive
          clearTimeout(timer)
          this.pending.delete(id)
          reject(err)
        }
      }),
    }
  }

  sendMessage(chatChannel: ChatChannel, payload: any, timeoutMs = 5000): AckPromise {
    const message: WebSocketMessage = {
      channel: chatChannel.id,
      payload: JSON.stringify(payload),
      type: WSMessageType.ChatMessage,
    }
    return this.sendWithAck(message, timeoutMs)
  }

  subscribe(channel: string, callback: (payload: WebSocketMessage) => void) {
    this.subscriptions[channel] = callback
    const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe }
    this.webSocketService.send(JSON.stringify(message))
  }

  unsubscribe(channel: string, callback: (payload: WebSocketMessage) => void) {
    delete this.subscriptions[channel]
    const message: WebSocketMessage = { channel, type: WSMessageType.Unsubscribe }
    this.webSocketService.send(JSON.stringify(message))
  }

  onMessage(message: WebSocketMessage) {
    const key = message.channel
    this.subscriptions[key!]?.(message)
  }

  addPendingSubscription(channel: string, callback: (payload: WebSocketMessage) => void): void {
    this.pendingSubscriptions[channel] = callback
  }

  subscribePendingSubscriptions(): void {
    // Pending callbacks become active subscriptions. A pending callback replaces an
    // existing one for the same channel, because it was registered later.
    this.subscriptions = { ...this.subscriptions, ...this.pendingSubscriptions }
    this.pendingSubscriptions = {}

    // (Re)subscribe every channel once: after a reconnection the server has forgotten them
    for (const channel of Object.keys(this.subscriptions)) {
      const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe }
      this.webSocketService.send(JSON.stringify(message))
    }
  }

  // Connects (or reconnects) when not connected. It is also used for the first connection,
  // so it must not depend on isActive: the automatic reconnection checks that on its own.
  async tryReconnect(): Promise<void> {
    if (this.connected.value) return
    await this.connect(async () => {
      this.subscribePendingSubscriptions()
    })
  }
}

const wsMessageService = new WebSocketMessageService()
const recconnectInterval = 3000

let reconnectTimer: ReturnType<typeof setInterval> | undefined = undefined

function stopReconnecting() {
  clearInterval(reconnectTimer)
  reconnectTimer = undefined
}

async function reconnectAttempt() {
  // disconnect() may be called while reconnecting
  if (!wsMessageService.isActive) return stopReconnecting()
  try {
    await wsMessageService.tryReconnect()
  } catch (err) {
    // The next attempt runs on the next interval
  }
}

watch(wsMessageService.connected, async (connected) => {
  if (connected || !wsMessageService.isActive) {
    stopReconnecting()
    return
  }

  if (!reconnectTimer) {
    // Start the interval before the first attempt, so a failed attempt does not stop it
    reconnectTimer = setInterval(reconnectAttempt, recconnectInterval)
    await reconnectAttempt()
  }
})

export default wsMessageService
