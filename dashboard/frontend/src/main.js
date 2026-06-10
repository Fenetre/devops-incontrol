import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { addCollection } from '@iconify/vue'
import heroicons from '@iconify-json/heroicons/icons.json'
import router from './router/index.js'
import App from './App.vue'
import { vAutofocus } from './directives/autofocus.js'
import ui from '@nuxt/ui/vue-plugin'
import './assets/main.css'

// Register heroicons locally so icons work offline (no CDN fetch)
addCollection(heroicons)

document.addEventListener('dragstart', e => e.preventDefault())

const app = createApp(App)
app.directive('autofocus', vAutofocus)
app.use(createPinia())
app.use(router)
app.use(ui)
app.mount('#app')
