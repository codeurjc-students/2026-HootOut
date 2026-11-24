import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from '@/App.vue'
import router from '@/router'
import vTooltip from '@/components/shared/tooltip/vTooltip.ts'
import { loadConfig } from './config'
import { setupPresence } from './services/presence/PresenceSync'

await loadConfig()
const app = createApp(App)
app.directive('tooltip', vTooltip)
app.use(createPinia())
setupPresence(router)
app.use(router)

app.mount('#app')
