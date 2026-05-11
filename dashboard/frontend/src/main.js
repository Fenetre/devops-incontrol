import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router/index.js'
import App from './App.vue'
import { vAutofocus } from './directives/autofocus.js'
import './assets/tailwind.css'

// Prevent all drag-and-drop gestures (stops accidental back/forward navigation)
document.addEventListener('dragstart', e => e.preventDefault())

const app = createApp(App)
app.directive('autofocus', vAutofocus)
app.use(createPinia())
app.use(router)
app.mount('#app')
