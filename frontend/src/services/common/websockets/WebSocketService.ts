export interface WebSocketHandlers {
  onOpen?: (event: Event) => void
  onClose?: (event: Event) => void
  onError?: (event: Event) => void
  onMessage?: (event: MessageEvent) => void
}

export class WebSocketService {
  private socket: WebSocket | null = null

  connect(url: string, handlers: WebSocketHandlers = {}): void {
    // Close the previous socket, if any, so its events never reach the new handlers
    this.close()

    this.socket = new WebSocket(url)
    this.socket.onopen = (event) => handlers.onOpen?.(event)
    this.socket.onclose = (event) => handlers.onClose?.(event)
    this.socket.onerror = (event) => handlers.onError?.(event)
    this.socket.onmessage = (event) => handlers.onMessage?.(event)
  }

  send(data: string): void {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      throw new Error('WebSocket is not connected')
    }
    this.socket.send(data)
  }

  close(): void {
    if (!this.socket) return

    // The browser fires onclose asynchronously after close(). The handlers are
    // detached first so an intentional close does not reach them: onClose is
    // only called when the server or the network closes the connection.
    this.socket.onopen = null
    this.socket.onclose = null
    this.socket.onerror = null
    this.socket.onmessage = null

    this.socket.close()
    this.socket = null
  }

  isConnected(): boolean {
    return this.socket?.readyState === WebSocket.OPEN
  }
}

export default new WebSocketService()
