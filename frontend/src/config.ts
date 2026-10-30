export interface AppConfig {
  API_URL: string
  WSS_URL: string
}

let config: AppConfig | null = null

export async function loadConfig(): Promise<void> {
  const res = await fetch('/config.json', { cache: 'no-store' })
  if (!res.ok) throw new Error(`Couldn't load config.json (${res.status})`)
  config = await res.json()
}

export function getConfig(): AppConfig {
  if (!config) throw new Error('The config is not loaded')
  return config
}
